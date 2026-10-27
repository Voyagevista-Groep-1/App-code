export async function getInvoices(search, status, page) {
    const query = new URLSearchParams({ search: search ?? "", status, page: String(page) });
    const response = await fetch(`/api/finance/invoices?${query}`, {
        credentials: "same-origin",
        cache: "no-store",
        headers: { Accept: "application/json" }
    });
    const body = response.headers.get("content-type")?.includes("application/json")
        ? await response.json() : null;
    return { status: response.status, data: response.ok ? body : null, error: body?.error ?? null };
}
