async function request(path, options = {}) {
    const response = await fetch(path, {
        credentials: "same-origin",
        cache: "no-store",
        ...options,
        headers: { Accept: "application/json", ...options.headers }
    });
    const body = response.headers.get("content-type")?.includes("application/json")
        ? await response.json() : null;
    return { status: response.status, data: response.ok ? body : null, error: body?.error ?? null };
}

export function getUsers(search) {
    return request(`/api/admin/users?search=${encodeURIComponent(search ?? "")}`);
}

export function getRoles() {
    return request("/api/admin/roles");
}

export async function saveRoles(userId, roles) {
    const token = await request("/api/admin/antiforgery");
    if (token.status !== 200) return token;
    return request(`/api/admin/users/${userId}/roles`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", RequestVerificationToken: token.data.token },
        body: JSON.stringify({ roles })
    });
}
