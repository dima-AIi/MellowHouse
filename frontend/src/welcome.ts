const standalone =
  matchMedia("(display-mode:standalone)").matches ||
  (navigator as Navigator & { standalone?: boolean }).standalone === true;
const navigationType = (
  performance.getEntriesByType("navigation")[0] as
    | PerformanceNavigationTiming
    | undefined
)?.type;
if (
  standalone &&
  navigationType === "navigate" &&
  !document.referrer &&
  /^\/Menu\/?$/i.test(location.pathname) &&
  !location.search &&
  !location.hash
)
  location.replace("/");
if (location.pathname === "/") {
  let repeat = true;
  try {
    repeat = sessionStorage.getItem("mh.hero.v5") === "1";
    sessionStorage.setItem("mh.hero.v5", "1");
  } catch {}
  document.documentElement.classList.add(
    repeat ? "mh-welcome-return" : "mh-welcome-first",
  );
  setTimeout(
    () => document.documentElement.classList.add("mh-welcome-ready"),
    5000,
  );
}
