# Mellow House Vercel entry point

Import dima-AIi/MellowHouse into a separate Vercel project, select Framework Preset Other and Root Directory deploy/vercel-proxy. No environment variables or Node dependencies are needed here. The configuration forwards pages, API, forms and media to https://mellow-house.onrender.com while retaining the public Vercel address. Personalized responses must not be cached.

Database credentials and the AI key belong only in Render Environment. Do not import App_Data/render.env into Vercel. The ASP.NET Core server runs on Render, and PostgreSQL lives in Neon. Existing portfolio projects are independent.
