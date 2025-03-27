using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using Hai.HView.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Hai.HView.VRCLogin;

/// I hate this<br/>
/// DANGER: This is a class that deals with sensitive information.
/// Exercise extreme caution when printing information to the output logs.
public class HVVrcSession
{
    // https://github.com/vrchatapi/specification/commit/558c0ca50202c45194a49d515f27e64f62079ba4#diff-5fa520d3bb34f9ae444cdbdf2b9eccff2361eb89a0cd3f4dba1e2e0fa9bba452R15
    // https://discord.com/channels/418093857394262020/418512124529344523/1303873667473866752
    // "Yes, going forward, all API requests need to go through api.vrchat.cloud instead"
    private const string RootUrl = "https://api.vrchat.cloud/api/1"; // Formerly: "https://vrchat.com/api/1"
    private const string CookieDomainBit = "vrchat.com";
    private const string CookieDomain = $"https://{CookieDomainBit}";
    private const string AuthUrl = RootUrl + "/auth/user";
    private const string LogoutUrl = RootUrl + "/logout";
    private const string EmailOtpUrl = RootUrl + "/auth/twofactorauth/emailotp/verify";
    private const string OtpUrl = RootUrl + "/auth/twofactorauth/otp/verify";
    public bool IsLoggedIn => _isLoggedIn;

    private static string SwitchAvatarUrl(string safe_avatarId) => $"{RootUrl}/avatars/{safe_avatarId}/select";
    
    private CookieContainer _cookies;
    private HttpClient _client;
    private bool _isLoggedIn;

    public HVVrcSession()
    {
        _cookies = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies
        };
        _client = new HttpClient(handler);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd($"Hai.HView/{VERSION.version} (docs.hai-vr.dev/docs/products/h-view#user-agent)");
    }

    public string GetAllCookies__Sensitive()
    {
        if (!ConditionalCompilation.CookiesSupported) throw new InvalidOperationException("GetAllCookies__Sensitive should not be invoked when cookies are disabled in conditional compilation.");
        
        return JsonConvert.SerializeObject(CompileCookies());
    }

    public void ProvideCookies(string userinput_cookies__sensitive)
    {
        if (!ConditionalCompilation.CookiesSupported) return;
        
        _cookies = new CookieContainer();
        var deserialized = JsonConvert.DeserializeObject<VrcAuthenticationCookies>(userinput_cookies__sensitive);
        if (deserialized.auth != null) _cookies.Add(new Uri(CookieDomain), RebuildCookie(deserialized.auth, "auth"));
        if (deserialized.twoFactorAuth != null) _cookies.Add(new Uri(CookieDomain), RebuildCookie(deserialized.twoFactorAuth, "twoFactorAuth"));
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies
        };
        _client = new HttpClient(handler);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd($"Hai.HView/{VERSION.version} (docs.hai-vr.dev/docs/products/h-view)");
        
        // Assume that if the user has an auth cookie, then they're logged in.
        // There is a route to check if the token is still valid, but for privacy, we don't want the application to send a request
        // to VRChat's server every time we start.
        // VRChat should only be privileged to know when a user is actively using HView when that user is actively
        // using the avatar switching function.
        _isLoggedIn = deserialized.auth != null;
    }

    private static Cookie RebuildCookie(VrcCookie cookie, string name)
    {
        return new Cookie
        {
            Domain = CookieDomainBit,
            Name = name,
            Value = cookie.Value,
            Expires = cookie.Expires,
            HttpOnly = true,
            Path = "/"
        };
    }

    private VrcAuthenticationCookies CompileCookies()
    {
        var subCookies = _cookies.GetCookies(new Uri(CookieDomain)).ToArray();
        var authNullable = subCookies.Where(cookie => cookie.Name == "auth").Select(Cookify).FirstOrDefault();
        var twoFactorAuthNullable = subCookies.Where(cookie => cookie.Name == "twoFactorAuth").Select(Cookify).FirstOrDefault();
        
        return new VrcAuthenticationCookies
        {
            auth = authNullable,
            twoFactorAuth = twoFactorAuthNullable
        };
    }

    private VrcCookie Cookify(Cookie cookie)
    {
        return new VrcCookie
        {
            Value = cookie.Value,
            Expires = cookie.Expires,
        };
    }

    [Serializable]
    public class VrcAuthenticationCookies
    {
        public VrcCookie auth;
        public VrcCookie twoFactorAuth;
    }

    [Serializable]
    public class VrcCookie
    {
        public string Value;
        public DateTime Expires;
    }

    public async Task<LogoutResponseStatus> Logout()
    {
        if (!_isLoggedIn) return LogoutResponseStatus.NotLoggedIn;
        _isLoggedIn = false;
        
        var request = new HttpRequestMessage(HttpMethod.Put, LogoutUrl);
        
        var response = await _client.SendAsync(request);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => LogoutResponseStatus.Success,
            HttpStatusCode.Unauthorized => LogoutResponseStatus.Unauthorized,
            _ => LogoutResponseStatus.OutsideProtocol
        };
    }
    
    public async Task<LoginResponse> Login(string userinput_account__sensitive, string userinput_password__sensitive)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, AuthUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", EncodeBasicAuth__Sensitive(userinput_account__sensitive, userinput_password__sensitive));
        
        var response = await _client.SendAsync(request);
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ParseLoginOk(response),
            HttpStatusCode.Unauthorized => new LoginResponse { Status = LoginResponseStatus.Failure },
            _ => new LoginResponse { Status = LoginResponseStatus.OutsideProtocol }
        };
    }

    private async Task<LoginResponse> ParseLoginOk(HttpResponseMessage response)
    {
        // The response will Set-Cookie onto our client, if we don't have it already.
        var content = await response.Content.ReadAsStringAsync();
        var hasTwofer = JObject.Parse(content).TryGetValue("requiresTwoFactorAuth", out JToken twoferMethod);
        if (hasTwofer)
        {
            return new LoginResponse
            {
                Status = LoginResponseStatus.RequiresTwofer,
                TwoferMethod = twoferMethod.Values<string>().Contains("emailOtp") ? TwoferMethod.Email : TwoferMethod.Other
            };
        }

        // This happens if our request also had the twofer cookie
        _isLoggedIn = true;
        return new LoginResponse
        {
            Status = LoginResponseStatus.Success
        };
    }

    public async Task<LoginResponse> VerifyTwofer(string userinput_twoferCode__sensitive, TwoferMethod method)
    {
        // TODO: Sanitize the user input
        
        // Our client has the auth cookie that was set as a result of a successful auth that lead to a twofer.
        var request = new HttpRequestMessage(HttpMethod.Post, method == TwoferMethod.Email ? EmailOtpUrl : OtpUrl);
        request.Content = new StringContent(JObject.FromObject(new TwoferRequestPayload
        {
            code = userinput_twoferCode__sensitive
        }).ToString(), Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));
        
        var response = await _client.SendAsync(request);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ParseVerifyOk(response),
            HttpStatusCode.Unauthorized => new LoginResponse { Status = LoginResponseStatus.Failure },
            _ => new LoginResponse { Status = LoginResponseStatus.OutsideProtocol }
        };
    }

    private async Task<LoginResponse> ParseVerifyOk(HttpResponseMessage response)
    {
        // The response will Set-Cookie the twofer when successful.
        var content = await response.Content.ReadAsStringAsync();
        var hasVerified = JObject.Parse(content).TryGetValue("verified", out JToken verifyResult);
        if (hasVerified)
        {
            _isLoggedIn = true;
            return new LoginResponse
            {
                Status = verifyResult.Value<bool>() ? LoginResponseStatus.Success : LoginResponseStatus.Failure
            };
        }

        return new LoginResponse
        {
            Status = LoginResponseStatus.OutsideProtocol
        };
    }

    public async Task<SwitchAvatarResponseStatus> SelectAvatar(string userinput_avatarId)
    {
        if (!_isLoggedIn) return SwitchAvatarResponseStatus.NotLoggedIn;
        
        var avatarId = HttpUtility.UrlEncode(userinput_avatarId);
        var request = new HttpRequestMessage(HttpMethod.Put, SwitchAvatarUrl(avatarId));
        
        var response = await _client.SendAsync(request);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => SwitchAvatarResponseStatus.Success,
            HttpStatusCode.Unauthorized => SwitchAvatarResponseStatus.Unauthorized,
            HttpStatusCode.NotFound => SwitchAvatarResponseStatus.NotFound,
            _ => SwitchAvatarResponseStatus.OutsideProtocol
        };
    }

    private string EncodeBasicAuth__Sensitive(string userinput_account__sensitive, string userinput_password__sensitive)
    {
        var basicToken__sensitive = $"{HttpUtility.UrlEncode(userinput_account__sensitive)}:{HttpUtility.UrlEncode(userinput_password__sensitive)}";
        var bytes__sensitive = Encoding.UTF8.GetBytes(basicToken__sensitive);
        var result__sensitive = Convert.ToBase64String(bytes__sensitive);
        return result__sensitive;
    }

    public enum LogoutResponseStatus
    {
        Unresolved, OutsideProtocol, Success, Unauthorized, NotLoggedIn
    }

    public struct LoginResponse
    {
        public LoginResponseStatus Status;
        public TwoferMethod TwoferMethod;
    }

    public enum LoginResponseStatus
    {
        Unresolved, OutsideProtocol, Failure, Success, RequiresTwofer
    }

    public enum TwoferMethod
    {
        Other, Email
    }

    [Serializable]
    public class TwoferRequestPayload
    {
        // ReSharper disable once InconsistentNaming
        public string code;
    }

    public enum SwitchAvatarResponseStatus
    {
        Unresolved, OutsideProtocol, Success, NotFound, Unauthorized, NotLoggedIn
    }
}