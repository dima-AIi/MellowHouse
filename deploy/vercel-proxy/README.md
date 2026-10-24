# Vercel static storefront with Render API

Vercel project: mellow-house. Repository: dima-AIi/MellowHouse. Root Directory: deploy/vercel-proxy. Framework: Other. vercel.json runs node build.mjs; no packages or private environment variables are needed. Keep Include source files outside of the Root Directory in the Build Step enabled (the Vercel default).

build.mjs creates Build Output API v3 under .vercel/output. Public HTML pages, products, fonts, video, CSS, JavaScript and images are served directly by Vercel, independent of Render startup. APIs, admin pages, health and missing assets still proxy to Render with no-store headers.

Only anonymous public pages and the public catalog/settings are exported. Form antiforgery inputs are removed. Profiles, carts, orders, cookies, CSRF tokens and admin pages are never written into the build. Account/Checkout/Payment/Order HTML contains interface shells only; private information comes from authenticated APIs. Browser URLs and same-origin authentication remain intact.

The bundled catalog supports immediate browsing, search and product configuration. Startup establishes the real session cookie before fetching live catalog data; current catalog/settings then update the interface. New product IDs use a generic product shell, and archived products disappear after live refresh. Current prices, availability and order rules are always validated by the backend. Safe startup GETs retry temporary network/cold-start failures for up to three minutes. Mutations are never automatically repeated.

Render Free still sleeps. Login, cart changes, orders and the assistant may wait for startup, with a small connection notice. Before live connection the catalog is the publication snapshot. Admin edits appear after API refresh; redeploy Vercel after menu changes to refresh the initial snapshot too. This is not an always-on free backend.

The builder fetches public Render pages at build time. If Render cannot provide valid pages, the build fails rather than publishing a loading screen or empty site. The last successful Vercel deployment remains available. Credentials and AI keys belong only in Render Environment; never upload App_Data/render.env to Vercel. Existing portfolio projects stay unchanged.

Check from repo root: node --test tools/static-storefront-v13.test.mjs. Generated .vercel output is ignored by Git.
