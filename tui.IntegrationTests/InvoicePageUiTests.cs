using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using tui.Components.Pages.Finance;
using tui.Services;
using Xunit;

namespace tui.IntegrationTests;

// Exercise the actual Razor events and lifecycle with a controlled browser API response.
// HTTP authorization and SQL behavior are covered separately by the MariaDB tests.
#pragma warning disable BL0006 // Render-tree access is confined to this test renderer.
public class InvoicePageUiTests
{
    [Theory]
    [InlineData(0, "Er zijn nog geen facturen geregistreerd")]
    [InlineData(7, "Geen facturen gevonden")]
    public async Task Empty_states_show_the_required_message(int totalInvoices, string message)
    {
        var api = new BrowserApi { Respond = _ => Task.FromResult(new Invoices.ApiResponse(200, new InvoicePage([], 1, 25, 0, totalInvoices), null)) };
        await using var view = await PageRenderer.StartAsync(api);
        await view.WaitForAsync(message);
    }

    [Fact]
    public async Task Loading_error_and_retry_show_saved_invoice_fields()
    {
        var pending = new TaskCompletionSource<Invoices.ApiResponse>();
        var api = new BrowserApi { Respond = _ => pending.Task };
        await using var view = await PageRenderer.StartAsync(api);
        Assert.Contains("Facturen laden", await view.TextAsync());
        pending.SetResult(new(503, null, "De facturen konden niet worden opgehaald. Probeer het opnieuw."));
        await view.WaitForAsync("Opnieuw proberen");
        api.Respond = request => Task.FromResult(Success(request));
        await view.ClickAsync("Opnieuw proberen");
        await view.WaitForAsync("INV-1");
        var text = await view.TextAsync();
        Assert.Contains("Orbit Tours", text);
        Assert.Contains("27-10-2026", text);
        Assert.Contains("1.234,56", text);
        Assert.Contains("Openstaand", text);
        Assert.DoesNotContain("Opnieuw proberen", text);
    }

    [Fact]
    public async Task Paging_keeps_filters_and_changes_or_clearing_restart_at_page_one()
    {
        var api = new BrowserApi();
        await using var view = await PageRenderer.StartAsync(api);
        await view.WaitForAsync("INV-1");
        Assert.Equal(new Request("", "Alle", 1), api.Calls.Last());
        await view.ClickAsync("Volgende");
        Assert.Equal(new Request("", "Alle", 2), api.Calls.Last());
        await view.ChangeAsync("oninput", "Orbit");
        Assert.Equal(new Request("Orbit", "Alle", 1), api.Calls.Last());
        await view.ClickAsync("Volgende");
        Assert.Equal(new Request("Orbit", "Alle", 2), api.Calls.Last());
        await view.ChangeAsync("onchange", "Betaald");
        Assert.Equal(new Request("Orbit", "Betaald", 1), api.Calls.Last());
        await view.ClickAsync("Volgende");
        Assert.Equal(new Request("Orbit", "Betaald", 2), api.Calls.Last());
        await view.ClickAsync("Wissen");
        Assert.Equal(new Request("", "Betaald", 1), api.Calls.Last());
    }

    [Fact]
    public async Task Slow_old_search_response_does_not_replace_newer_results()
    {
        var old = new TaskCompletionSource<Invoices.ApiResponse>();
        var started = new TaskCompletionSource();
        var api = new BrowserApi
        {
            Respond = request =>
            {
                if (request.Search == "old") { started.TrySetResult(); return old.Task; }
                return Task.FromResult(Success(request));
            }
        };
        await using var view = await PageRenderer.StartAsync(api);
        await view.WaitForAsync("INV-1");
        var previous = view.ChangeAsync("oninput", "old");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await view.ChangeAsync("oninput", "new");
        old.SetResult(new(200, new InvoicePage([], 1, 25, 0, 56), null));
        await previous;
        Assert.Contains("INV-1", await view.TextAsync());
        Assert.DoesNotContain("Geen facturen gevonden", await view.TextAsync());
        Assert.Equal("new", api.Calls.Last().Search);
    }

    private record Request(string Search, string Status, int Page);
    private static Invoices.ApiResponse Success(Request request) => new(200,
        new InvoicePage([new(1, "INV-1", "Orbit Tours", new DateOnly(2026, 10, 27), 1234.56m, "Openstaand")], request.Page, 25, 56, 56), null);

    private sealed class BrowserApi : IJSRuntime, IJSObjectReference
    {
        public List<Request> Calls { get; } = [];
        public Func<Request, Task<Invoices.ApiResponse>> Respond { get; set; } = request => Task.FromResult(Success(request));
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import") return (TValue)(object)this;
            var request = new Request((string)args![0]!, (string)args[1]!, (int)args[2]!);
            Calls.Add(request);
            return (TValue)(object)await Respond(request);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/finance/invoices");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class FinanceAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, AccessPolicies.Finance)], "Test"))));
    }

    private sealed class PageRenderer : Renderer
    {
        private readonly ServiceProvider _services;
        private int _pageId;
        private Exception? _failure;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        private PageRenderer(ServiceProvider services) : base(services, services.GetRequiredService<ILoggerFactory>()) => _services = services;
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => _failure = exception;

        public static async Task<PageRenderer> StartAsync(BrowserApi api)
        {
            var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(api).AddSingleton<NavigationManager>(new TestNavigation())
                .AddAuthorizationCore(AccessPolicies.Configure).AddCascadingAuthenticationState()
                .AddSingleton<AuthenticationStateProvider, FinanceAuthentication>().BuildServiceProvider();
            var renderer = new PageRenderer(services);
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                renderer._pageId = renderer.AssignRootComponentId(renderer.InstantiateComponent(typeof(Invoices)));
                await renderer.RenderRootComponentAsync(renderer._pageId);
            });
            return renderer;
        }

        private RenderTreeFrame[] Frames()
        {
            if (_failure is not null) throw new InvalidOperationException("Page rendering failed", _failure);
            var frames = GetCurrentRenderTreeFrames(_pageId);
            return frames.Array.Take(frames.Count).ToArray();
        }
        public Task<string> TextAsync() => Dispatcher.InvokeAsync(() => string.Join(" ", Frames().Select(frame => frame.FrameType switch
        {
            RenderTreeFrameType.Text => frame.TextContent,
            RenderTreeFrameType.Markup => frame.MarkupContent,
            _ => ""
        })));
        public Task ChangeAsync(string eventName, string value) => Dispatcher.InvokeAsync(() =>
            DispatchEventAsync(Frames().First(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == eventName).AttributeEventHandlerId,
                null, new ChangeEventArgs { Value = value }));
        public Task ClickAsync(string label) => Dispatcher.InvokeAsync(() =>
        {
            var frames = Frames();
            for (var index = 0; index < frames.Length; index++)
            {
                if (frames[index].FrameType != RenderTreeFrameType.Element || frames[index].ElementName != "button") continue;
                var button = frames.Skip(index + 1).Take(frames[index].ElementSubtreeLength - 1).ToArray();
                if (button.Any(frame => frame.FrameType == RenderTreeFrameType.Text && frame.TextContent == label))
                    return DispatchEventAsync(button.First(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick").AttributeEventHandlerId,
                        null, new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
            }
            throw new InvalidOperationException($"Button not found: {label}");
        });
        public async Task WaitForAsync(string text)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < timeout)
            {
                if ((await TextAsync()).Contains(text)) return;
                await Task.Delay(10);
            }
            Assert.Contains(text, await TextAsync());
        }
        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _services.DisposeAsync();
        }
    }
}
#pragma warning restore BL0006
