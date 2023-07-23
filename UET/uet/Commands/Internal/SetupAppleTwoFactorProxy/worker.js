export default {
    async fetch(request, env, ctx) {
        if (request.method == "GET") {
            const { searchParams } = new URL(request.url);
            if (searchParams.get('number') != env.UET_INBOUND_PHONE_NUMBER) {
                return new Response("The requester is not authorized to access this endpoint.", { status: 403 });
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
        else if (request.method == "POST") {
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
        else {
            return new Response("Method not supported.", { status: 405 });
        }
    },
};

