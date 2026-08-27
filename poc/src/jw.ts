import { USER_AGENT } from "./cas.js";
import {
  CookieJar,
  fetchWithJar,
  followRedirects,
} from "./http.js";

const JW_BASE = "https://jw.cdut.edu.cn";

export interface CurriculumPreInfo {
  sjmsValue: string;
  xqids: string[];
  availableWeeks: Record<string, string>;
}

export async function authenticateJw(jar: CookieJar): Promise<void> {
  const result = await followRedirects(jar, `${JW_BASE}/sso/login.jsp`, {
    headers: { "User-Agent": USER_AGENT },
  });
  if (result.steps >= 15) {
    throw new Error("教务系统认证重定向次数过多");
  }
}

export async function getCurriculumPreInfo(
  jar: CookieJar
): Promise<CurriculumPreInfo> {
  const res = await fetchWithJar(
    jar,
    `${JW_BASE}/jsxsd/framework/xsMainV_new.htmlx?t1=1`,
    {
      headers: { "User-Agent": USER_AGENT },
    }
  );
  const html = await res.text();

  const sjmsMatch = html.match(/data-value="(.*)" name="kbjcmsid"/);
  const sjmsValue = sjmsMatch?.[1] ?? "";

  const xqids: string[] = [];
  const xqidRegex = /<option value="">([\d-]*)<\/option>/g;
  let m: RegExpExecArray | null;
  while ((m = xqidRegex.exec(html)) !== null) {
    if (m[1]) xqids.push(m[1]);
  }

  const availableWeeks: Record<string, string> = {};
  const weekRegex = /<option value="([\d-]+)"(?:.*)>(.*)<\/option>/g;
  while ((m = weekRegex.exec(html)) !== null) {
    availableWeeks[m[2]] = m[1];
  }

  return { sjmsValue, xqids, availableWeeks };
}

export async function getWeekScheduleRaw(
  jar: CookieJar,
  sjms: string,
  xqid: string,
  weekId: string
): Promise<string> {
  const url = `${JW_BASE}/jsxsd/framework/mainV_index_loadkb.htmlx?rq=${encodeURIComponent(
    weekId
  )}&sjmsValue=${encodeURIComponent(sjms)}&xnxqid=${encodeURIComponent(
    xqid
  )}&xswk=true`;
  const res = await fetchWithJar(jar, url, {
    headers: { "User-Agent": USER_AGENT },
  });
  return res.text();
}

export interface ClassInfo {
  className: string;
  teacher: string;
  location: string;
  classWeek: string;
  classSchedule: string;
}

export function parseSchedule(rawHtml: string): ClassInfo[] {
  const cellRegex =
    /<td align="left">\r?\n\s*\r?\n([\s\S]*?)\r?\n\r?\n\s*<\/td>/g;
  const cells: string[] = [];
  let m: RegExpExecArray | null;
  while ((m = cellRegex.exec(rawHtml)) !== null) {
    cells.push(m[1]);
  }

  const results: ClassInfo[] = [];
  const infoPattern =
    /<span onmouseover='kbtc\(this\)' onmouseout='kbot\(this\)' class='box' style='[^']*'><p>[^<]*<\/p><p>([^<]*)<\/p><span class='text'>([^<]*)<\/span><\/span><div class='item-box' ><p>(\S*)<\/p><div class='tch-name'><span>(\S*?)<\/span><span>([^<]*)<\/span><\/div><div><span><img src='\/jsxsd\/assets_v1\/images\/item1.png'>([^<]*)/;

  for (const cell of cells) {
    if (!cell.trim()) continue;
    const match = cell.match(infoPattern);
    if (!match) continue;
    results.push({
      className: match[3],
      teacher: match[1],
      classWeek: match[2],
      classSchedule: match[5],
      location: match[6],
    });
  }
  return results;
}
