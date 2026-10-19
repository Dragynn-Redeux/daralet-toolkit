using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// A request to the Market API that runs off the game's thread. The window reads Result, Error and Busy each frame; the
    /// request sets them when it finishes (a reference write, so the window never sees half of one).
    /// </summary>
    public class ApiRequest<T> where T : class {
        private volatile T result;
        private volatile string error;
        private volatile bool busy;
        private int generation;

        public T Result => result;
        public string Error => error;
        public bool Busy => busy;

        /// <summary>When the last result came, or MinValue.</summary>
        public DateTime UpdatedAt { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Starts a request. A newer one wins: if an older one finishes later, its result is dropped.
        /// </summary>
        public void Start(Func<Task<T>> fetch) {
            var mine = Interlocked.Increment(ref generation);
            busy = true;
            error = null;

            Task.Run(async () => {
                try {
                    var value = await fetch().ConfigureAwait(false);

                    if (mine == generation) {
                        result = value;
                        UpdatedAt = DateTime.UtcNow;
                    }
                }
                catch (Exception ex) {
                    if (mine == generation) {
                        error = (ex is AggregateException aggregate ? aggregate.InnerException ?? ex : ex).Message;
                    }
                }
                finally {
                    if (mine == generation) {
                        busy = false;
                    }
                }
            });
        }
    }

    /// <summary>
    /// The public Market API the website uses (daralet-ACE apps/market-api), for market search and the leaderboards: read-only
    /// and the same for everyone, so it's asked directly over HTTPS rather than through the game server.
    /// </summary>
    public class MarketApi : IDisposable {
        /// <summary>Where the website is; nginx passes /api/ through to the Market API.</summary>
        public const string DefaultBaseUrl = "https://play.daralet.ac";

        private readonly HttpClient client;

        static MarketApi() {
            // .NET Framework 4.8 doesn't always offer TLS 1.2 unless asked, and the site needs it
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public MarketApi(string baseUrl = DefaultBaseUrl) {
            client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Daralet/0.1");
        }

        /// <summary>GETs a path ("api/market/listings?...") and reads the JSON reply.</summary>
        public async Task<T> GetJson<T>(string pathAndQuery) {
            using (var response = await client.GetAsync(pathAndQuery.TrimStart('/')).ConfigureAwait(false)) {
                if (!response.IsSuccessStatusCode) {
                    throw new HttpRequestException($"The Market API answered {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return Json.Read<T>(json);
            }
        }

        public Task<MarketSearchResult> SearchListings(string query) {
            return GetJson<MarketSearchResult>("api/market/listings?" + query);
        }

        /// <summary>One of the boards in Leaderboards.All, by the path its definition makes.</summary>
        public Task<List<LeaderboardRow>> GetLeaderboard(string path) {
            return GetJson<List<LeaderboardRow>>(path);
        }

        public void Dispose() {
            client.Dispose();
        }
    }
}
