using Conesoft.Blazor.NetatmoAuth.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Conesoft.Blazor.NetatmoAuth;

public partial class NetatmoAuthorization : ComponentBase, IDisposable
{
    static readonly string apiUrl = "https://api.netatmo.com";

    [Inject(Key = "netatmo")] public IStorage Storage { get; set; } = default!;
    [Inject] IHttpClientFactory Factory { get; set; } = default!;
    [Inject] NavigationManager Navigation { get; set; } = default!;
    [Inject] PersistentComponentState ApplicationState { get; set; } = default!;

    [CascadingParameter] HttpContext? HttpContext { get; set; }

    [Parameter] public string ClientId { get; set; } = default!;
    [Parameter] public string ClientSecret { get; set; } = default!;
    [Parameter] public string Scopes { get; set; } = default!;
    [Parameter] public RenderFragment? NotAuthorized { get; set; } = default;
    [Parameter] public RenderFragment<AuthUser>? Authorized { get; set; } = default;

    [SupplyParameterFromQuery(Name = "state")] public string? State { get; set; }
    [SupplyParameterFromQuery(Name = "code")] public string? Code { get; set; }

    AuthSteps CurrentAuthStep { get; set; } = AuthSteps.NotAuthorized;
    AuthUser User { get; set; } = new("", "");
    string AuthorizeNavLink { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        CurrentAuthStep = AuthSteps.NotAuthorized;

        if (State != null && Code != null)
        {
            CurrentAuthStep = AuthSteps.AuthorizingWithCode;
        }
        if (Storage.Exists("token") == true)
        {
            CurrentAuthStep = AuthSteps.Authorized;
        }

        switch (CurrentAuthStep)
        {
            case AuthSteps.NotAuthorized:
                {
                    string state = UrlEncoder.Default.Encode(Guid.NewGuid().ToString());

                    await Storage.Write("state", state);

                    AuthorizeNavLink = $"{apiUrl}/oauth2/authorize?client_id={ClientId}&redirect_uri={GetRedirectUri()}&scope={UrlEncoder.Default.Encode(Scopes!)}&state={state}";
                }
                break;

            case AuthSteps.AuthorizingWithCode:
                {
                    var authstate = await Storage.Read("state");
                    Storage!.Remove("state");
                    if (authstate != State)
                    {
                        return;
                    }
                    var http = Factory.CreateClient();
                    var request = await http.PostAsync($"{apiUrl}/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>()
                    {
                        ["grant_type"] = "authorization_code",
                        ["client_id"] = ClientId!,
                        ["client_secret"] = ClientSecret!,
                        ["code"] = Code!,
                        ["redirect_uri"] = GetRedirectUri(),
                        ["scope"] = Scopes!,
                    }));
                    var token = await request.Content.ReadFromJsonAsync<AuthToken>();

                    await Storage.Write("token", JsonSerializer.Serialize(token));

                    // remove the query parameters from the url
                    Navigation.NavigateTo(GetRedirectUri(), forceLoad: true);
                }
                break;

            case AuthSteps.Authorized:
                {
                    // load user information
                    var token = JsonSerializer.Deserialize<AuthToken>(await Storage.Read("token"));
                    if(token == null)
                    {
                        return;
                    }
                    var http = Factory.CreateClient();
                    http.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
                    http.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
                    var result = await http.GetFromJsonAsync<StationsData.Result>($"{apiUrl}/api/getstationsdata");
                    if(result == null)
                    {
                        return;
                    }
                    var user = result.Body.User.Mail.Split('@').First();
                    var home = result.Body.Devices.First().StationName.Split('(').First().Trim();
                    User = new(user, home);
                }
                break;
        }
    }

    public void RestartAuthorization()
    {
        Storage!.Remove("token");
        Navigation.NavigateTo(GetRedirectUri(), forceLoad: true);
    }

    private PersistingComponentStateSubscription persistingSubscription;
    public void Dispose() => persistingSubscription.Dispose();
    private string GetRedirectUri()
    {
        var redirect_uri = Navigation.Uri.Split("?").First();

        persistingSubscription = ApplicationState.RegisterOnPersisting(() =>
        {
            ApplicationState.PersistAsJson(nameof(redirect_uri), redirect_uri);
            return Task.CompletedTask;
        });

        if (!ApplicationState.TryTakeFromJson<string>(nameof(redirect_uri), out var restored))
        {
            if (
                HttpContext?.Request.Headers.TryGetValue("X-Forwarded-Host", out var hostvalue) == true && hostvalue.Count == 1 && hostvalue.FirstOrDefault() is string host
                &&
                HttpContext?.Request.Headers.TryGetValue("X-Forwarded-Proto", out var protocolvalue) == true && protocolvalue.Count == 1 && protocolvalue.FirstOrDefault() is string protocol
            )
            {
                redirect_uri = new UriBuilder(redirect_uri)
                {
                    Host = host,
                    Port = protocol.EndsWith('s') ? 443 : 80
                }.Uri.ToString();
            }
        }
        else
        {
            redirect_uri = restored!;
        }
        return redirect_uri;
    }
}
