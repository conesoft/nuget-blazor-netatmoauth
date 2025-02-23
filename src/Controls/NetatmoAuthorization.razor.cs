using Conesoft.Blazor.NetatmoAuth.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Netatmo;
using NodaTime;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Conesoft.Blazor.NetatmoAuth;

public partial class NetatmoAuthorization : ComponentBase, IDisposable
{
    static string ApiUrl => "https://api.netatmo.com";

    [Parameter] public string ClientId { get; set; } = default!;
    [Parameter] public string ClientSecret { get; set; } = default!;
    [Parameter] public string Scopes { get; set; } = default!;
    [Parameter] public RenderFragment? NotAuthorized { get; set; } = default;
    [Parameter] public RenderFragment<AuthUser>? Authorized { get; set; } = default;

    [Inject(Key = "netatmo")] public IStorage Storage { get; set; } = default!;

    [Inject] IHttpClientFactory Factory { get; set; } = default!;
    [Inject] NavigationManager Navigation { get; set; } = default!;

    [CascadingParameter] HttpContext? HttpContext { get; set; }

    [SupplyParameterFromQuery(Name = "state")] public string? State { get; set; }
    [SupplyParameterFromQuery(Name = "code")] public string? Code { get; set; }

    enum AuthSteps { NotAuthorized, AuthorizedWithCode, AuthorizedWithoutToken, AuthorizedWithToken }
    AuthSteps CurrentAuthStep { get; set; } = AuthSteps.NotAuthorized;
    AuthUser User { get; set; } = new("", "");
    string AuthorizeNavLink { get; set; } = "";

    [Inject] PersistentComponentState ApplicationState { get; set; } = default!;

    private PersistingComponentStateSubscription persistingSubscription;

    public void Dispose() => persistingSubscription.Dispose();

    protected override async Task OnInitializedAsync()
    {
        if (State == null && Code == null && Storage!.Exists("authorization") == false)
        {
            CurrentAuthStep = AuthSteps.NotAuthorized;
        }
        if (State != null && Code != null)
        {
            CurrentAuthStep = AuthSteps.AuthorizedWithCode;
        }
        if (Storage!.Exists("authorization") == true)
        {
            CurrentAuthStep = AuthSteps.AuthorizedWithoutToken;
        }
        if (Storage!.Exists("token") == true)
        {
            CurrentAuthStep = AuthSteps.AuthorizedWithToken;
        }

        switch (CurrentAuthStep)
        {
            case AuthSteps.NotAuthorized:
                {
                    string state = UrlEncoder.Default.Encode(Guid.NewGuid().ToString());

                    await Storage!.Write("state", JsonSerializer.Serialize(new AuthState(state)));

                    var redirect_uri = Navigation.Uri;


                    persistingSubscription = ApplicationState.RegisterOnPersisting(() =>
                    {
                        ApplicationState.PersistAsJson("redirect_uri", redirect_uri);
                        return Task.CompletedTask;
                    });

                    if (!ApplicationState.TryTakeFromJson<string>("redirect_uri", out var restored))
                    {
                        if (HttpContext?.Request.Headers.TryGetValue("X-Forwarded-Host", out var value) == true && value.Count == 1)
                        {
                            redirect_uri = new UriBuilder(Navigation.Uri)
                            {
                                Host = value.First()
                            }.Uri.ToString();
                        }
                    }
                    else
                    {
                        redirect_uri = restored!;
                    }

                    AuthorizeNavLink = $"{ApiUrl}/oauth2/authorize?client_id={ClientId}&scope={UrlEncoder.Default.Encode(Scopes!)}&redirect_uri={redirect_uri}&state={state}";
                }
                break;

            case AuthSteps.AuthorizedWithCode:
                {
                    var authstate = JsonSerializer.Deserialize<AuthState>(await Storage!.Read("state"))!;
                    Storage!.Remove("state");
                    if (authstate.State == State!)
                    {
                        await Storage!.Write("authorization", JsonSerializer.Serialize<AuthCode>(new(Code: Code!, State: State!)));
                    }

                    Navigation.NavigateTo(Navigation.Uri.Split("?").First(), forceLoad: true);
                }
                break;

            case AuthSteps.AuthorizedWithoutToken:
                {
                    var authcode = JsonSerializer.Deserialize<AuthCode>(await Storage!.Read("authorization"))!;
                    Code = authcode.Code;
                    State = authcode.State;
                    Storage!.Remove("authorization");

                    var http = Factory.CreateClient();
                    var request = await http.PostAsync($"{ApiUrl}/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>()
                    {
                        ["grant_type"] = "authorization_code",
                        ["client_id"] = ClientId!,
                        ["client_secret"] = ClientSecret!,
                        ["code"] = Code!,
                        ["redirect_uri"] = Navigation.Uri,
                        ["scope"] = Scopes!,
                    }));
                    var token = await request.Content.ReadFromJsonAsync<AuthToken>();

                    await Storage!.Write("token", JsonSerializer.Serialize(token));

                    Navigation.NavigateTo(Navigation.Uri.Split("?").First(), forceLoad: true);
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
        Navigation.NavigateTo(AuthorizeNavLink, forceLoad: true);
    }

    public void RestartAuthorization()
    {
        Storage!.Remove("token");
        Navigation.NavigateTo(Navigation.Uri.Split("?").First(), forceLoad: true);
    }
}
