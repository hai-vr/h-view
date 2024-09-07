using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using Hai.HView.Core;
using Newtonsoft.Json.Linq;

namespace Hai.HView.VRCLogin;

/// I hate this<br/>
/// This is a sensitive class. Exercise extreme caution when outputting information to the output logs.
public class HVVrcSession
{
    private const string RootUrl = "https://vrchat.com/api/1";
    private const string AuthUrl = RootUrl + "/auth/user";
    private const string EmailOtpUrl = RootUrl + "/auth/twofactorauth/emailotp/verify";
    private const string OtpUrl = RootUrl + "/auth/twofactorauth/otp/verify";
    private static string SwitchAvatarUrl(string safe_avatarId) => $"{RootUrl}/avatars/{safe_avatarId}/select";
    
    // TODO: Persist the auth cookie / the twofer cookie across app restarts
    private readonly HttpClient _client;
    private bool _isLoggedIn;

    public HVVrcSession()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd($"Hai.HView/{VERSION.version} (docs.hai-vr.dev/docs/products/h-view)");
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