async function post(url, body={}) {
    const csrf = getCookie("csrf_token");

    return await fetch(url, {
        method: "POST",
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-Token": csrf
        },
        body: JSON.stringify(body)
    });
}
async function get(url) {
    const csrf = getCookie("csrf_token");

    return await fetch(url, {
        method: "GET",
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-Token": csrf
        }
    });
}
function getCookie(name) {
    return document.cookie
        .split("; ")
        .find(row => row.startsWith(name + "="))
        ?.split("=")[1];
}