# RAAMS frontend

The development sign-in accepts a `dotnet user-jwts` token and stores it in
browser local storage. This is a development shim, not MSAL or a production
authentication flow. The browser does not verify the JWT signature; the API
does. Do not use real or sensitive credentials with this development flow.

From the API project directory, create a token with a GUID subject and an
email claim (required by `/api/auth/me`):

```powershell
dotnet user-jwts create --name <guid> --role GRA --role SuperAdmin --claim email=you@example.com
```

Paste the generated token into the sign-in page. The API maps the token's
`unique_name` claim to the display name. The API must be running separately
at `https://localhost:7014`; accept its development HTTPS certificate in the
browser by visiting `https://localhost:7014/api/auth/ping` once. That endpoint
requires authentication, so an unauthorized response after accepting the
certificate is expected.

Run `npm run build` to type-check and build the frontend.
