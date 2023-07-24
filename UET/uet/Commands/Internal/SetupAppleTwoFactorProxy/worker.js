/**
 * @param {URL} url
 * @param {Request} request
 * @param {any} env
 * @returns {Response}
 */
async function getTwoFactorCode(url, request, env) {
    if (url.searchParams.get('number') != env.UET_INBOUND_PHONE_NUMBER) {
        return new Response("The requester is not authorized to access this endpoint.", { status: 403 });
    }
    if (url.searchParams.has('sessionId')) {
        var desiredSessionId = url.searchParams.get('sessionId');
        var currentSessionId = env.KV.get('current-session');
        if (currentSessionId != desiredSessionId) {
            return new Response("The requester is not the current session holder.", { status: 403 });
        }
    }
    const code = await env.KV.get("apple-2fa");
    if (code == null) {
        return new Response(
            "There is no 2FA code available at this time. Please refresh this page in a few seconds to see if a 2FA code has arrived.",
            {
                status: 404
            });
    } else {
        // Delete the cached 2FA code because it will be invalid once used by the caller.
        await env.KV.delete("apple-2fa");
        return new Response(code, { status: 200 });
    }
}

/**
 * @param {URL} url
 * @param {Request} request
 * @param {any} env
 */
async function saveIncomingTwoFactorCode(url, request, env) {
    const sms = await request.formData();
    await env.KV.put("apple-2fa-debug", JSON.stringify(Array.from(sms)));
    if (sms.get("To") != env.UET_INBOUND_PHONE_NUMBER) {
        return new Response("SMS was received by the wrong phone number.", { status: 400 });
    }
    const match = /\#([0-9]{6})/.exec(sms.get("Text"));
    if (match == null) {
        return new Response("SMS did not contain a 2FA code.", { status: 400 });
    }
    await env.KV.put("apple-2fa", match[1]);
    return new Response("2FA code was successfully stored for later retrieval.", { status: 200 });
}

/**
 * @param {URL} url
 * @param {Request} request
 * @param {any} env
 */
async function setCurrentSession(url, request, env) {
    if (url.searchParams.get('number') != env.UET_INBOUND_PHONE_NUMBER) {
        return new Response("The requester is not authorized to access this endpoint.", { status: 403 });
    }
    var desiredSessionId = url.searchParams.get('sessionId');
    var currentSessionId = env.KV.get('current-session');
    if (currentSessionId == desiredSessionId) {
        return new Response("2FA endpoint is currently reserved for you.", { status: 200 });
    } else if (currentSessionId == null) {
        env.KV.put('current-session', desiredSessionId, { expirationTtl: 300 });
        return new Response("2FA endpoint reserved for session " + desiredSessionId + ".", { status: 200 });
    } else {
        return new Response("The 2FA endpoint is currently reserved by another session. Try again later.", { status: 409 });
    }
}

export default {
    async fetch(request, env, ctx) {
        var url = new URL(request.url);
        switch (url.pathname) {
            case "/":
                switch (request.method) {
                    case "GET":
                        return await getTwoFactorCode(url, request, env);
                    case "POST":
                        return await saveIncomingTwoFactorCode(url, request, env);
                }
                break;
            case "/session":
                switch (request.method) {
                    case "POST":
                        return await setCurrentSession(url, request, env);
                }
                break;
        }

        return new Response("Method or URL not supported.", { status: 405 });
    },
};

