# Mellow House Vercel entry point

Import dima-AIi/MellowHouse into a separate Vercel project, select Framework Preset Other and Root Directory deploy/vercel-proxy. No environment variables or Node dependencies are needed here. The configuration forwards pages, API, forms and media to https://mellow-house.onrender.com while retaining the public Vercel address. Personalized responses must not be cached.

Database credentials and the AI key belong only in Render Environment. Do not import App_Data/render.env into Vercel. The ASP.NET Core server runs on Render, and PostgreSQL lives in Neon. Existing portfolio projects are independent.

The catch-all `routes` rule intentionally runs before filesystem handling. A high-level rewrite left `/` resolving against the empty static output directory and returning Vercel NOT_FOUND, while `/health` and `/api/catalog` already reached Render. The explicit route proxies the empty path as well as all nested paths and retains the no-store response policy.
