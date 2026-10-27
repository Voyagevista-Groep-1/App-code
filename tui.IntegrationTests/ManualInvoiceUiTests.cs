using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using tui.Components.Pages.Finance;
using tui.Services;
using Xunit;

namespace tui.IntegrationTests;

#pragma warning disable BL0006 // Actual Razor lifecycle/events are exercised through a test renderer.
public class ManualInvoiceUiTests
{
    [Fact]
    public async Task User_can_select_receiver_edit_reorder_remove_lines_and_save_then_see_readonly_summary()
    {
        var api = new BrowserApi(); await using var view = await FormRenderer.StartAsync(api);
        await view.WaitForAsync("Bestaande ontvanger");
        await FillHeaderAsync(view);
        await view.ChangeAsync("description-0", "Advies"); await view.ChangeAsync("price-0", "100");
        await view.WaitForAsync("121,00");
        await view.ClickAsync("Regel toevoegen");
        await view.ChangeAsync("description-1", "Boeken"); await view.ChangeAsync("price-1", "10"); await view.ChangeAsync("vat-1", "9");
        await view.ClickAsync("Regel 2 omhoog");
        Assert.Equal("Boeken", await view.ValueAsync("description-0"));
        Assert.Equal("Advies", await view.ValueAsync("description-1"));
        await view.ClickAsync("Verwijderen", occurrence: 1);
        Assert.Equal("Boeken", await view.ValueAsync("description-0"));
        await view.SubmitAsync();
        await view.WaitForAsync("succesvol opgeslagen");
        Assert.Equal("Boeken", Assert.Single(api.Saved!.Lines!).Description);
        Assert.Equal(9, api.Saved.Lines![0].VatPercentage);
        Assert.Equal(10.90m, ManualInvoiceCalculator.Calculate(api.Saved.Currency, api.Saved.Lines).TotalIncludingVat);
        Assert.Contains("REG-TEST-1", await view.TextAsync());
        Assert.Contains("BRON-UI-001", await view.TextAsync());
        Assert.Contains("Openstaand", await view.TextAsync());
        Assert.DoesNotContain("Factuur opslaan", await view.TextAsync());
    }

    [Fact]
    public async Task Server_field_error_keeps_entered_values_and_can_be_corrected_and_resubmitted()
    {
        var api = new BrowserApi { Reject = true }; await using var view = await FormRenderer.StartAsync(api);
        await view.WaitForAsync("Bestaande ontvanger"); await FillHeaderAsync(view);
        await view.ChangeAsync("description-0", "Originele dienst"); await view.ChangeAsync("price-0", "50");
        await view.SubmitAsync(); await view.WaitForAsync("Deze factuur is al geregistreerd");
        Assert.Equal("BRON-UI-001", await view.ValueAsync("original-number"));
        Assert.Equal("Originele dienst", await view.ValueAsync("description-0"));
        Assert.Equal("50", await view.ValueAsync("price-0"));
        api.Reject = false;
        await view.ChangeAsync("original-number", "BRON-UI-002"); await view.SubmitAsync();
        await view.WaitForAsync("succesvol opgeslagen");
        Assert.Equal("BRON-UI-002", api.Saved!.OriginalInvoiceNumber);
    }

    [Fact]
    public async Task Empty_header_or_missing_lines_never_calls_save_api()
    {
        var api = new BrowserApi(); await using var view = await FormRenderer.StartAsync(api);
        await view.WaitForAsync("Bestaande ontvanger"); await view.SubmitAsync();
        Assert.Equal(0, api.SaveCalls);
        await FillHeaderAsync(view); await view.ClickAsync("Verwijderen"); await view.SubmitAsync();
        await view.WaitForAsync("minimaal één geldige factuurregel");
        Assert.Equal(0, api.SaveCalls);
    }

    [Fact]
    public async Task Failed_lookup_can_be_retried_and_vat_explanation_is_required()
    {
        var api = new BrowserApi { LookupFails = true }; await using var view = await FormRenderer.StartAsync(api);
        await view.WaitForAsync("Ontvangers niet beschikbaar"); api.LookupFails = false;
        await view.ClickAsync("Opnieuw proberen"); await view.WaitForAsync("Bestaande ontvanger"); await FillHeaderAsync(view);
        await view.ChangeAsync("description-0", "Vrijgestelde dienst"); await view.ChangeAsync("price-0", "50");
        await view.ChangeAsync("vat-0", "exempt"); await view.SubmitAsync();
        await view.WaitForAsync("grond voor vrijstelling"); Assert.Equal(0, api.SaveCalls);
        await view.ChangeAsync("vat-info-0", "Vrijstellingsgrond bronfactuur"); await view.SubmitAsync();
        await view.WaitForAsync("succesvol opgeslagen"); Assert.Equal("exempt", api.Saved!.Lines![0].VatTreatment);
    }

    private static async Task FillHeaderAsync(FormRenderer view)
    {
        await view.ChangeAsync("recipient", "1"); await view.ChangeAsync("sender-name", "Leverancier BV");
        await view.ChangeAsync("sender-address", "Straat 1, Utrecht"); await view.ChangeAsync("original-number", "BRON-UI-001");
        await view.ChangeAsync("invoice-date", "2026-10-28");
    }

    private sealed class BrowserApi : IJSRuntime, IJSObjectReference
    {
        public bool Reject { get; set; }
        public bool LookupFails { get; set; }
        public int SaveCalls { get; private set; }
        public ManualInvoiceRequest? Saved { get; private set; }
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object response;
            if (identifier == "import") response = this;
            else if (identifier == "getOptions") response = LookupFails
                ? new InvoiceRegistration.RegistrationResponse<ManualInvoiceOptions>(503, null, "Ontvangers niet beschikbaar", null)
                : new InvoiceRegistration.RegistrationResponse<ManualInvoiceOptions>(200, new([new(1, "VoyageVista", "Adres 1, Amsterdam", "NL123456789B01")], ["EUR", "USD"]), null, null);
            else
            {
                SaveCalls++; Saved = (ManualInvoiceRequest)args![0]!;
                response = Reject ? new InvoiceRegistration.RegistrationResponse<ManualInvoiceDetail>(409, null, "Controleer de invoer", new() { ["originalInvoiceNumber"] = ["Deze factuur is al geregistreerd"] })
                    : new InvoiceRegistration.RegistrationResponse<ManualInvoiceDetail>(201,
                        new(1, "REG-TEST-1", "Openstaand", DateTime.UtcNow, Saved, new(1, "VoyageVista", "Adres 1, Amsterdam", "NL123456789B01"),
                            ManualInvoiceCalculator.Calculate(Saved.Currency, Saved.Lines), ManualInvoiceCalculator.Policy), null, null);
            }
            return ValueTask.FromResult((T)response);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/finance/invoices/new");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class EmployeeAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, AccessPolicies.InvoiceEmployee)], "Test"))));
    }

    private sealed class FormRenderer : Renderer
    {
        private readonly ServiceProvider _services;
        private int _root;
        private Exception? _failure;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        private FormRenderer(ServiceProvider services) : base(services, services.GetRequiredService<ILoggerFactory>()) => _services = services;
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => _failure = exception;
        public static async Task<FormRenderer> StartAsync(BrowserApi api)
        {
            var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.Services.AddRazorComponents();
            builder.Services.AddAuthorizationCore(AccessPolicies.Configure).AddCascadingAuthenticationState();
            builder.Services.RemoveAll<AuthenticationStateProvider>();
            builder.Services.AddSingleton<AuthenticationStateProvider, EmployeeAuthentication>();
            builder.Services.RemoveAll<IJSRuntime>(); builder.Services.RemoveAll<NavigationManager>();
            builder.Services.AddSingleton<IJSRuntime>(api).AddSingleton<NavigationManager>(new TestNavigation());
            var renderer = new FormRenderer(builder.Services.BuildServiceProvider());
            await renderer.Dispatcher.InvokeAsync(async () => { renderer._root = renderer.AssignRootComponentId(renderer.InstantiateComponent(typeof(InvoiceRegistration))); await renderer.RenderRootComponentAsync(renderer._root); });
            return renderer;
        }
        private IEnumerable<RenderTreeFrame[]> Trees(int? id = null)
        {
            if (_failure is not null) throw new InvalidOperationException("Form rendering failed", _failure);
            var range = GetCurrentRenderTreeFrames(id ?? _root); var frames = range.Array.Take(range.Count).ToArray(); yield return frames;
            foreach (var component in frames.Where(frame => frame.FrameType == RenderTreeFrameType.Component))
                foreach (var children in Trees(component.ComponentId)) yield return children;
        }
        private static string Text(IEnumerable<RenderTreeFrame> frames) => string.Join(" ", frames.Select(frame => frame.FrameType switch { RenderTreeFrameType.Text => frame.TextContent, RenderTreeFrameType.Markup => frame.MarkupContent, _ => "" }));
        public Task<string> TextAsync() => Dispatcher.InvokeAsync(() => Text(Trees().SelectMany(tree => tree)));
        private RenderTreeFrame[] Element(string id) => Trees().SelectMany(tree => Enumerable.Range(0, tree.Length)
            .Where(index => tree[index].FrameType == RenderTreeFrameType.Element)
            .Select(index => tree.Skip(index + 1).Take(tree[index].ElementSubtreeLength - 1).ToArray()))
            .First(frames => frames.TakeWhile(frame => frame.FrameType == RenderTreeFrameType.Attribute).Any(frame => frame.AttributeName == "id" && Equals(frame.AttributeValue, id)));
        public Task<string> ValueAsync(string id) => Dispatcher.InvokeAsync(() => Element(id).First(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "value").AttributeValue?.ToString() ?? "");
        public Task ChangeAsync(string id, object value) => Dispatcher.InvokeAsync(() => DispatchEventAsync(Element(id).First(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName is "onchange" or "oninput").AttributeEventHandlerId, null, new ChangeEventArgs { Value = value }));
        public Task ClickAsync(string label, int occurrence = 0) => Dispatcher.InvokeAsync(() =>
        {
            var buttons = Trees().SelectMany(tree => Enumerable.Range(0, tree.Length)
                .Where(index => tree[index].FrameType == RenderTreeFrameType.Element && tree[index].ElementName == "button")
                .Select(index => tree.Skip(index + 1).Take(tree[index].ElementSubtreeLength - 1).ToArray()))
                .Where(frames => Text(frames).Contains(label) || frames.Any(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "aria-label" && Equals(frame.AttributeValue, label))).ToArray();
            return DispatchEventAsync(buttons[occurrence].First(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onclick").AttributeEventHandlerId, null, new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        });
        public Task SubmitAsync() => Dispatcher.InvokeAsync(() => DispatchEventAsync(Trees().SelectMany(tree => tree).First(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "onsubmit").AttributeEventHandlerId, null, EventArgs.Empty));
        public async Task WaitForAsync(string text)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < timeout) { if ((await TextAsync()).Contains(text)) return; await Task.Delay(10); }
            Assert.Contains(text, await TextAsync());
        }
        public new async ValueTask DisposeAsync() { await base.DisposeAsync(); await _services.DisposeAsync(); }
    }
}
#pragma warning restore BL0006
