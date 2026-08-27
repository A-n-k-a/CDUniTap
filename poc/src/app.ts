import { Hono } from "hono";
import { loginWithPassword } from "./cas.js";
import {
  authenticateJw,
  getCurriculumPreInfo,
  getWeekScheduleRaw,
  parseSchedule,
} from "./jw.js";
import { authenticatePaym, getAllProjects, getUserInfo } from "./paym.js";

const app = new Hono();

app.onError((err, c) => {
  console.error("[PoC] unhandled error:", err);
  const message = err instanceof Error ? err.message : String(err);
  const stack = err instanceof Error ? err.stack : undefined;
  return c.json(
    {
      error: "internal_server_error",
      message,
      ...(stack ? { stack: stack.split("\n").slice(0, 5) } : {}),
    },
    500
  );
});

app.get("/", (c) =>
  c.json({
    name: "CDUniTap PoC",
    description: "成都理工大学校园系统统一客户端 - Node.js 迁移验证",
    endpoints: [
      "GET /health",
      "GET /diag",
      "POST /auth/login",
      "POST /jw/schedule",
      "POST /paym/userinfo",
    ],
  })
);

app.get("/health", (c) => c.json({ ok: true, ts: Date.now() }));

app.get("/diag", async (c) => {
  const results: Record<string, unknown> = {};
  const targets = [
    { name: "cas", url: "https://cas.paas.cdut.edu.cn/cas/login" },
    { name: "jw-sso", url: "https://jw.cdut.edu.cn/sso/login.jsp" },
    { name: "jw-jsxsd", url: "https://jw.cdut.edu.cn/jsxsd/framework/xsMainV_new.htmlx?t1=1" },
    { name: "paym-casLogin", url: "https://paym.cdut.edu.cn/casLogin/" },
    { name: "paym-api", url: "https://paym.cdut.edu.cn/api/pay/project/getAllProjectList" },
  ];
  for (const t of targets) {
    const start = Date.now();
    try {
      const res = await fetch(t.url, {
        redirect: "manual",
        signal: AbortSignal.timeout(8000),
        headers: {
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36 Edg/116.0.1938.69",
        },
      });
      results[t.name] = {
        ok: res.ok || res.status === 302,
        status: res.status,
        location: res.headers.get("location"),
        ms: Date.now() - start,
      };
    } catch (err) {
      results[t.name] = {
        ok: false,
        error: err instanceof Error ? err.message : String(err),
        ms: Date.now() - start,
      };
    }
  }
  return c.json(results);
});

app.post("/auth/login", async (c) => {
  const body = await c.req.json<{ username: string; password: string }>();
  if (!body.username || !body.password) {
    return c.json({ error: "username 和 password 必填" }, 400);
  }
  const result = await loginWithPassword(body.username, body.password);
  if (!result.success) {
    return c.json(
      { success: false, message: "登录失败，请检查账号密码或验证码" },
      401
    );
  }
  return c.json({
    success: true,
    studentId: result.studentId,
  });
});

app.post("/jw/schedule", async (c) => {
  const body = await c.req.json<{ username: string; password: string }>();
  if (!body.username || !body.password) {
    return c.json({ error: "username 和 password 必填" }, 400);
  }

  const login = await loginWithPassword(body.username, body.password);
  if (!login.success) {
    return c.json({ error: "CAS 登录失败" }, 401);
  }

  await authenticateJw(login.jar);
  const preInfo = await getCurriculumPreInfo(login.jar);

  if (preInfo.xqids.length === 0 || !preInfo.sjmsValue) {
    return c.json(
      { error: "无法获取课表基础信息，可能是教务系统页面结构变化" },
      502
    );
  }

  const xqid = preInfo.xqids[0];
  const weekEntries = Object.entries(preInfo.availableWeeks);
  const firstWeek = weekEntries[0];
  if (!firstWeek) {
    return c.json({ error: "未找到可用周次" }, 502);
  }

  const raw = await getWeekScheduleRaw(
    login.jar,
    preInfo.sjmsValue,
    xqid,
    firstWeek[1]
  );
  const classes = parseSchedule(raw);

  return c.json({
    studentId: login.studentId,
    semester: xqid,
    week: firstWeek[0],
    classCount: classes.length,
    classes,
  });
});

app.post("/paym/userinfo", async (c) => {
  const body = await c.req.json<{ username: string; password: string }>();
  if (!body.username || !body.password) {
    return c.json({ error: "username 和 password 必填" }, 400);
  }

  const login = await loginWithPassword(body.username, body.password);
  if (!login.success) {
    return c.json({ error: "CAS 登录失败" }, 401);
  }

  try {
    const session = await authenticatePaym(login.jar);
    const [userInfo, projects] = await Promise.all([
      getUserInfo(login.jar, session),
      getAllProjects(login.jar, session),
    ]);
    return c.json({
      studentId: login.studentId,
      userInfo,
      projectCount: projects.length,
      projects,
    });
  } catch (err) {
    return c.json(
      {
        error: "paym 认证或数据获取失败",
        detail: err instanceof Error ? err.message : String(err),
      },
      502
    );
  }
});

export default app;
