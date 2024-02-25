using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Netatmo;
using NodaTime;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Conesoft.Blazor.NetdiscoAuth;

public partial class NetatmoAuthorization
{
    static string ApiUrl => "https://api.netatmo.com";

    [Parameter] public string ClientId { get; set; } = default!;
    [Parameter] public string ClientSecret { get; set; } = default!;
    [Parameter] public string Scopes { get; set; } = default!;
    [Parameter] public RenderFragment NotAuthorized { get; set; } = default!;
    [Parameter] public RenderFragment<AuthUser> Authorized { get; set; } = default!;

    [Inject(Key = "netatmo")] public IStorage Storage { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "state")] public string? State { get; set; }
    [SupplyParameterFromQuery(Name = "code")] public string? Code { get; set; }

    enum AuthSteps { NotAuthorized, AuthorizedWithCode, AuthorizedWithoutToken, AuthorizedWithToken }
    AuthSteps CurrentAuthStep { get; set; } = AuthSteps.NotAuthorized;
    AuthUser User { get; set; } = new("", "");
    string AuthorizeNavLink { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        if (State == null && Code == null && await Storage!.Exists("authorization") == false)
        {
            CurrentAuthStep = AuthSteps.NotAuthorized;
        }
        if (State != null && Code != null)
        {
            CurrentAuthStep = AuthSteps.AuthorizedWithCode;
        }
        if (await Storage!.Exists("authorization") == true)
        {
            CurrentAuthStep = AuthSteps.AuthorizedWithoutToken;
        }
        if (await Storage!.Exists("token") == true)
        {
            CurrentAuthStep = AuthSteps.AuthorizedWithToken;
        }

        switch (CurrentAuthStep)
        {
            case AuthSteps.NotAuthorized:
                {
                    string state = UrlEncoder.Default.Encode(Guid.NewGuid().ToString());

                    await Storage!.Write("state", JsonSerializer.Serialize(new AuthState(state)));

                    AuthorizeNavLink = $"{ApiUrl}/oauth2/authorize?client_id={ClientId}&scope={UrlEncoder.Default.Encode(Scopes!)}&redirect_uri={navigation.Uri}&state={state}";
                }
                break;

            case AuthSteps.AuthorizedWithCode:
                {
                    var authstate = JsonSerializer.Deserialize<AuthState>(await Storage!.Read("state"))!;
                    await Storage!.Remove("state");
                    if (authstate.State == State!)
                    {
                        await Storage!.Write("authorization", JsonSerializer.Serialize<AuthCode>(new(Code: Code!, State: State!)));
                    }
                    navigation.NavigateTo("/");
                }
                break;

            case AuthSteps.AuthorizedWithoutToken:
                {
                    var authcode = JsonSerializer.Deserialize<AuthCode>(await Storage!.Read("authorization"))!;
                    Code = authcode.Code;
                    State = authcode.State;
                    await Storage!.Remove("authorization");

                    var http = factory.CreateClient();
                    var request = await http.PostAsync($"{ApiUrl}/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>()
                    {
                        ["grant_type"] = "authorization_code",
                        ["client_id"] = ClientId!,
                        ["client_secret"] = ClientSecret!,
                        ["code"] = Code!,
                        ["redirect_uri"] = navigation.Uri,
                        ["scope"] = Scopes!,
                    }));
                    var token = await request.Content.ReadFromJsonAsync<AuthToken>();

                    await Storage!.Write("token", JsonSerializer.Serialize(token));

                    navigation.NavigateTo("/");
                }
                break;

            case AuthSteps.AuthorizedWithToken:
                {
                    var token = JsonSerializer.Deserialize<AuthToken>(await Storage!.Read("token"));

                    var client = new Client(SystemClock.Instance, ApiUrl, ClientId, ClientSecret);

                    client.ProvideOAuth2Token(token!.AccessToken, token!.RefreshToken);

                    try
                    {
                        var stationsData = await client.Weather.GetStationsData();
                        User = new(
                            Username: stationsData?.Body.User.Mail.Split("@").FirstOrDefault() ?? "",
                            Home: stationsData?.Body.Devices.FirstOrDefault()?.StationName.Split("(").FirstOrDefault()?.Trim() ?? ""
                            );
                    }
                    catch (Exception)
                    {
                    }
                }
                break;
        }
    }

    public void Authorize()
    {
        navigation.NavigateTo(AuthorizeNavLink, forceLoad: true);
    }

    public async Task RestartAuthorization()
    {
        await Storage!.Remove("token");
        navigation.NavigateTo("/", forceLoad: true);
    }
}
