using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Encoding = System.Text.Encoding;

namespace NeteaseMusicAPI
{
    public enum QualityLevel
    {
        STANDARD,           EXHIGH,             LOSSLESS,           HIRES,              SKY,                JYEFFECT,           JYMASTER,           DOLBY           }

    public class APIConstants
    {
        public static readonly byte[] AES_KEY = Encoding.UTF8.GetBytes("e82ckenh8dichen8");
        public static readonly string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Safari/537.36 Chrome/91.0.4472.164 NeteaseMusicDesktop/2.10.2.200154";
        public static readonly string REFERER = "https://music.163.com/";

        public static readonly string SONG_URL_V1 = "https://interface3.music.163.com/eapi/song/enhance/pass_player/url/v1";
        public static readonly string SONG_DETAIL_V3 = "https://interface3.music.163.com/api/v3/song/detail";
        public static readonly string LYRIC_API = "https://interface3.music.163.com/api/song/lyric";
        public static readonly string SEARCH_API = "https://music.163.com/api/cloudsearch/pc";
        public static readonly string PLAYLIST_DETAIL_API = "https://music.163.com/api/v6/playlist/detail";
        public static readonly string ALBUM_DETAIL_API = "https://music.163.com/api/v1/album/";
        public static readonly string QR_UNIKEY_API = "https://interface3.music.163.com/eapi/login/qrcode/unikey";
        public static readonly string QR_LOGIN_API = "https://interface3.music.163.com/eapi/login/qrcode/client/login";

                public static readonly Dictionary<string, string> DEFAULT_CONFIG = new()
        {
            {"os", "pc"},
            {"appver", ""},
            {"osver", ""},
            {"deviceId", "pyncm!"}
        };

        public static readonly Dictionary<string, string> DEFAULT_COOKIES = new()
        {
            {"os", "pc"},
            {"appver", ""},
            {"osver", ""},
            {"deviceId", "pyncm!"}
        };
    }

    public class CryptoUtils
    {
        public static string HexDigest(byte[] data)
        {
            StringBuilder sb = new();
            foreach (byte b in data)
            {
                _ = sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public static byte[] HashDigest(string text)
        {
            using var md5 = MD5.Create();
            return md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        public static string HashHexDigest(string text)
        {
            return HexDigest(HashDigest(text));
        }

        public static string EncryptParams(string url, object payload)
        {
            string urlPath = new Uri(url).AbsolutePath.Replace("/eapi/", "/api/");
            string payloadJson = JsonConvert.SerializeObject(payload);
            string digest = HashHexDigest($"nobody{urlPath}use{payloadJson}md5forencrypt");
            string paramsStr = $"{urlPath}-36cd479b6b5-{payloadJson}-36cd479b6b5-{digest}";
            byte[] encrypted = AESEncryptECB(Encoding.UTF8.GetBytes(paramsStr), APIConstants.AES_KEY);
            return HexDigest(encrypted);
        }

        private static byte[] AESEncryptECB(byte[] toEncrypt, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(toEncrypt, 0, toEncrypt.Length);
        }
    }

    public class APIException : Exception
    {
        public APIException(string message) : base(message) { }
    }

    public class SongUrlResponse
    {
        public List<SongUrlData> data { get; set; }
        public int code { get; set; }
    }

    public class SongUrlData
    {
        public long id { get; set; }
        public string url { get; set; }
        public int br { get; set; }                   public long size { get; set; }                public string md5 { get; set; }
        public int code { get; set; }
        public int expi { get; set; }                 public string type { get; set; }              public double gain { get; set; }              public double peak { get; set; }              public double closedGain { get; set; }
        public double closedPeak { get; set; }
        public int fee { get; set; }                  public object uf { get; set; }                public int payed { get; set; }                public int flag { get; set; }
        public bool canExtend { get; set; }           public object freeTrialInfo { get; set; }
        public string level { get; set; }             public string encodeType { get; set; }        public object channelLayout { get; set; }
        public FreeTrialPrivilege freeTrialPrivilege { get; set; }
        public FreeTimeTrialPrivilege freeTimeTrialPrivilege { get; set; }
        public int urlSource { get; set; }
        public int rightSource { get; set; }
        public object podcastCtrp { get; set; }
        public object effectTypes { get; set; }
        public int time { get; set; }                 public object message { get; set; }
        public object levelConfuse { get; set; }
        public object accompany { get; set; }
        public int sr { get; set; }                   public object auEff { get; set; }
        public object immerseType { get; set; }
        public int beatType { get; set; }
        public string musicId { get; set; }
    }
    public class FreeTrialPrivilege
    {
        public bool resConsumable { get; set; }
        public bool userConsumable { get; set; }
        public object listenType { get; set; }
        public object cannotListenReason { get; set; }
        public object playReason { get; set; }
        public object freeLimitTagType { get; set; }
    }

    public class FreeTimeTrialPrivilege
    {
        public bool resConsumable { get; set; }
        public bool userConsumable { get; set; }
        public int type { get; set; }
        public int remainTime { get; set; }
    }
    public class SongDetailResponse
    {
        public int code { get; set; }
        public List<SongDetail> songs { get; set; }
    }

    public class SongDetail
    {
        public long id { get; set; }
        public string name { get; set; }
        public List<Artist> ar { get; set; }
        public Album al { get; set; }
    }

    public class Artist
    {
        public string name { get; set; }
    }

    public class Album
    {
        public string name { get; set; }
        public string picUrl { get; set; }
    }

    public class LyricResponse
    {
        public int code { get; set; }
        public LyricInfo lrc { get; set; }
    }

    public class LyricInfo
    {
        public string lyric { get; set; }
    }

    public class SearchResult
    {
        public int code { get; set; }
        public SearchData result { get; set; }
    }

    public class SearchData
    {
        public List<SearchSong> songs { get; set; }
    }

    public class SearchSong
    {
        public long id { get; set; }
        public string name { get; set; }
        public List<Artist> ar { get; set; }
        public Album al { get; set; }
    }
    public class Result<T>
    {
        public Exception exception { get; set; }
        public T result { get; set; }
        public Result(Exception exception)
        {
            this.exception = exception;
        }
        public Result(T result)
        {
            this.result = result;
        }
    }
    public class NeteaseAPI
    {
        private readonly HttpClient httpClient;

        public NeteaseAPI()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", APIConstants.USER_AGENT);
            httpClient.DefaultRequestHeaders.Add("Referer", APIConstants.REFERER);
        }

        public async Task<Result<SongUrlResponse>> GetSongUrl(long songId, QualityLevel quality, Dictionary<string, string> cookies)
        {
            try
            {
                var config = new Dictionary<string, string>(APIConstants.DEFAULT_CONFIG)
                {
                    ["requestId"] = new Random().Next(20000000, 30000000).ToString()
                };

                var payload = new Dictionary<string, object>
                {
                    {"ids", new long[] { songId }},
                    {"level", quality.ToString().ToLower()},
                    {"encodeType", "flac"},
                    {"header", JsonConvert.SerializeObject(config)}
                };

                if (quality == QualityLevel.SKY)
                {
                    payload["immerseType"] = "c51";
                }

                string paramsStr = CryptoUtils.EncryptParams(APIConstants.SONG_URL_V1, payload);

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("params", paramsStr)
                });

                var requestCookies = new Dictionary<string, string>(APIConstants.DEFAULT_COOKIES);
                foreach (var cookie in cookies)
                {
                    requestCookies[cookie.Key] = cookie.Value;
                }

                string cookieHeader = string.Join("; ", requestCookies.Select(c => $"{c.Key}={c.Value}"));
                _ = httpClient.DefaultRequestHeaders.Remove("Cookie");
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

                HttpResponseMessage response = await httpClient.PostAsync(APIConstants.SONG_URL_V1, formContent);
                string responseText = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<SongUrlResponse>(responseText);
                return result.code != 200 ? throw new APIException($"获取歌曲URL失败: {result.code}") : new Result<SongUrlResponse>(result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new Result<SongUrlResponse>(e);

            }
        }

        public async Task<Result<SongDetailResponse>> GetSongDetail(long songId)
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    {"c", JsonConvert.SerializeObject(new[] { new { id = songId, v = 0 } })}
                };

                var formContent = new FormUrlEncodedContent(data.Select(d => new KeyValuePair<string, string>(d.Key, d.Value)));

                HttpResponseMessage response = await httpClient.PostAsync(APIConstants.SONG_DETAIL_V3, formContent);
                string responseText = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<SongDetailResponse>(responseText);
                return result.code != 200 ? throw new APIException($"获取歌曲详情失败: 未知错误") : new Result<SongDetailResponse>(result);
            }
            catch (Exception e)
            {
                throw new APIException($"获取歌曲详情失败: {e.Message}");
            }
        }

        public async Task<LyricResponse> GetLyric(long songId, Dictionary<string, string> cookies)
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    {"id", songId.ToString()},
                    {"cp", "false"},
                    {"tv", "0"},
                    {"lv", "0"},
                    {"rv", "0"},
                    {"kv", "0"},
                    {"yv", "0"},
                    {"ytv", "0"},
                    {"yrv", "0"}
                };

                var formContent = new FormUrlEncodedContent(data.Select(d => new KeyValuePair<string, string>(d.Key, d.Value)));

                var requestCookies = new Dictionary<string, string>(APIConstants.DEFAULT_COOKIES);
                foreach (var cookie in cookies)
                {
                    requestCookies[cookie.Key] = cookie.Value;
                }

                string cookieHeader = string.Join("; ", requestCookies.Select(c => $"{c.Key}={c.Value}"));
                _ = httpClient.DefaultRequestHeaders.Remove("Cookie");
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

                HttpResponseMessage response = await httpClient.PostAsync(APIConstants.LYRIC_API, formContent);
                string responseText = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<LyricResponse>(responseText);
                return result.code != 200 ? throw new APIException($"获取歌词失败: 未知错误") : result;
            }
            catch (Exception e)
            {
                throw new APIException($"获取歌词失败: {e.Message}");
            }
        }

        public async Task<List<Dictionary<string, object>>> SearchMusic(string keywords, Dictionary<string, string> cookies, int limit = 10)
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    {"s", keywords},
                    {"type", "1"},
                    {"limit", limit.ToString()}
                };

                var formContent = new FormUrlEncodedContent(data.Select(d => new KeyValuePair<string, string>(d.Key, d.Value)));

                var requestCookies = new Dictionary<string, string>(APIConstants.DEFAULT_COOKIES);
                foreach (var cookie in cookies)
                {
                    requestCookies[cookie.Key] = cookie.Value;
                }

                string cookieHeader = string.Join("; ", requestCookies.Select(c => $"{c.Key}={c.Value}"));
                _ = httpClient.DefaultRequestHeaders.Remove("Cookie");
                httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);

                HttpResponseMessage response = await httpClient.PostAsync(APIConstants.SEARCH_API, formContent);
                string responseText = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseText);
                var result = JsonConvert.DeserializeObject<SearchResult>(responseText);
                if (result.code != 200)
                {
                    throw new APIException($"搜索失败: 未知错误");
                }

                var songs = new List<Dictionary<string, object>>();
                if (result.result?.songs != null)
                {
                    foreach (var item in result.result.songs)
                    {
                        var songInfo = new Dictionary<string, object>
                        {
                            {"id", item.id},
                            {"name", item.name},
                            {"artists", string.Join("/", item.ar.Select(a => a.name))},
                            {"album", item.al.name},
                            {"picUrl", item.al.picUrl}
                        };
                        songs.Add(songInfo);
                    }
                }

                return songs;
            }
            catch (Exception e)
            {
                throw new APIException($"搜索失败: {e.Message}");
            }
        }

        public string GetPicUrl(long? picId, int size = 300)
        {
            if (!picId.HasValue)
            {
                return "";
            }

            string encId = NetEaseEncryptId(picId.Value.ToString());
            return $"https://p3.music.126.net/{encId}/{picId}.jpg?param={size}y{size}";
        }

        private string NetEaseEncryptId(string idStr)
        {
            char[] magic = "3go8&$8*3*3h0k(2)2".ToCharArray();
            char[] songId = idStr.ToCharArray();

            for (int i = 0; i < songId.Length; i++)
            {
                songId[i] = (char)(songId[i] ^ magic[i % magic.Length]);
            }

            string m = new(songId);
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(m));
            string result = Convert.ToBase64String(hash);
            result = result.Replace('/', '_').Replace('+', '-');
            return result;
        }

    }

}