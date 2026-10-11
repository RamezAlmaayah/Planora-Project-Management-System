# Browser regressions

Run against an existing local app with isolated test data, or explicitly select
the production URL. Tests do not create accounts, send mail, or submit mutations.
They verify auth viewport boundaries, icon dimensions, CSRF fields, asset bodies,
and browser-history restoration.

```powershell
npm ci --prefix tests/browser
$env:PLANORA_BASE_URL = 'http://localhost:5276'
npm test --prefix tests/browser
```

The browser configuration uses installed Chrome. Set `PLANORA_BASE_URL` to the
actual running app address. No browser authentication state is saved.
