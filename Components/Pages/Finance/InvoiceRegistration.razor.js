async function request(path, options = {}) {
    const response = await fetch(path, {
        credentials: "same-origin", cache: "no-store", ...options,
        headers: { Accept: "application/json", ...options.headers }
    });
    const body = response.headers.get("content-type")?.includes("application/json") ? await response.json() : null;
    return { status: response.status, data: response.ok ? body : null, error: body?.error ?? null, errors: body?.errors ?? {} };
}
export function getOptions() { return request("/api/finance/invoices/manual/options"); }
export function getInvoice(id) { return request(`/api/finance/invoices/manual/${id}`); }
export async function registerInvoice(invoice) {
    const token = await request("/api/finance/invoices/antiforgery");
    if (token.status !== 200) return token;
    return request("/api/finance/invoices/manual", {
        method: "POST", headers: { "Content-Type": "application/json", RequestVerificationToken: token.data.token },
        body: JSON.stringify(invoice)
    });
}
