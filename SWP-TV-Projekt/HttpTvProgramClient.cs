using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SWP_TV_Projekt
{
    public static class HttpTvProgramClient
    {
        private const string BaseUrl = "https://pilot.wp.pl/api/v2/epg";
        private static readonly HttpClient Client = new HttpClient();

        public static async Task<TvProgram> GetProgramAsync(int channel, int limit)
        {
            var channels = new List<int> {channel};
            return await GetProgramAsync(channels, limit).ConfigureAwait(false);
        }

        public static async Task<TvProgram> GetProgramAsync(IEnumerable<int> channels, int limit)
        {
            TvProgram program = null;
            var channelsParam = string.Join(",", channels);
            var path = $"{BaseUrl}?channels={channelsParam}&limit={limit}";
            var response = Client.GetAsync(new Uri(path)).Result;
            if (response.IsSuccessStatusCode)
                program = response.Content.ReadAsAsync<TvProgram>().Result;

            return await Task.FromResult(program).ConfigureAwait(false);
        }
    }
}