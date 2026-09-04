// Lumio Hello World browser client (MS-00002, role=browser).
//
// Wire truth is hello-wire-v1.json, fetched at runtime as ./contract.json (the integrator
// copies it next to this page). Required-field checks are driven dynamically from
// contract.messages.*.required — this script hardcodes no field list, so the browser never
// carries a second copy of the protocol.
//
// Machine-readable evidence: window.__lumioResult, shaped exactly like the contract's
// process.evidence.browserResult: {status, role, sessionId, baselineRevision,
// sent:{sequence,payloadSha256,sentAtMs}, received:[...], errors:[]}.

const result = {
  status: "running",
  role: "browser",
  sessionId: null,
  baselineRevision: null,
  sent: null,
  received: [],
  errors: [],
};
window.__lumioResult = result;

const dom = {
  status: document.getElementById("status"),
  statusDetail: document.getElementById("status-detail"),
  meta: document.getElementById("meta"),
  deltas: document.getElementById("deltas"),
  deltasEmpty: document.getElementById("deltas-empty"),
  errors: document.getElementById("errors"),
  errorsEmpty: document.getElementById("errors-empty"),
};

function renderStatus(status, detail) {
  dom.status.textContent = status;
  dom.statusDetail.textContent = detail ?? "";
}

function renderMeta(rows) {
  dom.meta.replaceChildren();
  for (const [key, value] of rows) {
    const dt = document.createElement("dt");
    dt.textContent = key;
    const dd = document.createElement("dd");
    dd.textContent = value;
    dom.meta.append(dt, dd);
  }
}

function setError(code, detail) {
  result.errors.push({ code, detail });
  result.status = "error";
  console.error(`[lumio-hello] ${code}: ${detail}`);
  renderStatus(result.status, detail);
  if (dom.errorsEmpty) {
    dom.errorsEmpty.remove();
  }
  const li = document.createElement("li");
  const codeElement = document.createElement("code");
  codeElement.textContent = code;
  li.append(codeElement, ` ${detail}`);
  dom.errors.append(li);
}

// ---------- 契约驱动的字段核对(文法:const:/enum:/u64/epoch-ms/sha256-hex/bool/string/array:) ----------

const sha256HexPattern = /^[0-9a-f]{64}$/;
// 渲染上限：收到的 delta 记录完整保留在 window.__lumioResult，表格只保留最新这些行。
const MAX_RENDERED_DELTAS = 200;

function makeValidator(contract) {
  const roles = Array.isArray(contract.roles) ? contract.roles : [];
  const sharedTypes = contract.sharedTypes ?? {};

  function checkField(target, field, constraint, problems) {
    if (typeof target !== "object" || target === null || !(field in target)) {
      problems.push({ field, problem: "missing required field" });
      return;
    }
    const value = target[field];
    if (constraint.startsWith("const:")) {
      if (value !== constraint.slice("const:".length)) {
        problems.push({ field, problem: `must equal const ${constraint.slice("const:".length)}` });
      }
    } else if (constraint === "enum:roles") {
      if (typeof value !== "string" || !roles.includes(value)) {
        problems.push({ field, problem: `must be one of roles(${roles.join("|")})` });
      }
    } else if (constraint === "u64" || constraint === "epoch-ms") {
      if (!Number.isInteger(value) || value < 0) {
        problems.push({ field, problem: `must be a non-negative integer(${constraint})` });
      }
    } else if (constraint === "sha256-hex") {
      if (typeof value !== "string" || !sha256HexPattern.test(value)) {
        problems.push({ field, problem: "must be 64 lowercase hex characters" });
      }
    } else if (constraint === "bool") {
      if (typeof value !== "boolean") {
        problems.push({ field, problem: "must be a boolean" });
      }
    } else if (constraint === "string") {
      if (typeof value !== "string") {
        problems.push({ field, problem: "must be a string" });
      }
    } else if (constraint.startsWith("array:")) {
      const shared = constraint.slice("array:".length);
      const elementRequired = sharedTypes[shared]?.required ?? null;
      if (!Array.isArray(value)) {
        problems.push({ field, problem: `must be an array of ${shared}` });
        return;
      }
      if (!elementRequired) {
        return;
      }
      for (const element of value) {
        if (typeof element !== "object" || element === null || Array.isArray(element)) {
          problems.push({ field, problem: `element must be a ${shared} object` });
          continue;
        }
        for (const [elementField, elementConstraint] of Object.entries(elementRequired)) {
          checkField(element, elementField, elementConstraint, problems);
        }
      }
    }
    // Unknown constraint grammar: presence-only, so additive contract evolution
    // does not make an old page reject new frames.
  }

  return function validate(messageType, message) {
    const problems = [];
    const required = contract.messages?.[messageType]?.required;
    if (!required) {
      problems.push({ field: "messageType", problem: `unknown_mapping: ${messageType}` });
      return problems;
    }
    if (typeof message !== "object" || message === null || Array.isArray(message)) {
      problems.push({ field: "$", problem: "not an object" });
      return problems;
    }
    for (const [field, constraint] of Object.entries(required)) {
      checkField(message, field, constraint, problems);
    }
    return problems;
  };
}

// 契约里的 const 记法(如 InputCommand.kind=const:hello)读出来用,不在脚本里写死。
function constValue(contract, messageType, field) {
  const constraint = contract.messages?.[messageType]?.required?.[field];
  return typeof constraint === "string" && constraint.startsWith("const:")
    ? constraint.slice("const:".length)
    : null;
}

async function sha256Hex(text) {
  const data = new TextEncoder().encode(text);
  const digest = await crypto.subtle.digest("SHA-256", data);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

// ---------- 主流程 ----------

async function main() {
  const params = new URLSearchParams(window.location.search);
  let serverUrl = params.get("ws");
  let launchCredential = null;
  result.role = params.get("role") || "browser";

  renderStatus("running", "waiting for contract.json …");

  let contract;
  try {
    const response = await fetch("./contract.json", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    contract = await response.json();
  } catch (error) {
    // 纯离线可开:契约不在场时停在 waiting,不产生错误噪音。
    renderStatus("running", `waiting for contract.json (${error.message})`);
    return;
  }

  if (typeof contract.contractId !== "string" || contract.contractId.length === 0) {
    setError("unsupported_contract", "contract.json lacks contractId");
    return;
  }

  const validate = makeValidator(contract);
  let subprotocol = contract.transport?.subprotocol ?? undefined;
  if (!serverUrl) {
    const parts = window.location.pathname.split("/").filter(Boolean);
    const gamesIndex = parts.indexOf("games");
    const slug = gamesIndex >= 0 ? parts[gamesIndex + 1] : null;
    if (!slug) {
      renderStatus("running", "waiting for game slug");
      return;
    }
    try {
      const launchResponse = await fetch(`/api/games/${encodeURIComponent(slug)}/launch`, {
        method: "POST",
        headers: { Accept: "application/json" },
        credentials: "same-origin",
      });
      if (!launchResponse.ok) throw new Error(`HTTP ${launchResponse.status}`);
      const launch = await launchResponse.json();
      if (typeof launch.wsUrl !== "string" || typeof launch.admissionCredential !== "string") {
        throw new Error("launch response lacks connection fields");
      }
      serverUrl = launch.wsUrl;
      launchCredential = launch.admissionCredential;
      if (typeof launch.subprotocol === "string") subprotocol = launch.subprotocol;
    } catch (error) {
      setError("launch_failed", `launch request failed: ${error.message}`);
      return;
    }
  }
  const metaState = {
    role: result.role,
    contractId: contract.contractId,
    subprotocol: subprotocol ?? "<missing>",
    server: serverUrl ?? "<not configured>",
    sessionId: null,
    baselineRevision: null,
    sentSequence: null,
    sentPayloadSha256: null,
  };

  function renderMetaState() {
    renderMeta([
      ["role", metaState.role],
      ["contractId", metaState.contractId],
      ["subprotocol", metaState.subprotocol],
      ["server", metaState.server],
      ["sessionId", metaState.sessionId ?? "—"],
      ["baselineRevision", metaState.baselineRevision ?? "—"],
      ["sent.sequence", metaState.sentSequence ?? "—"],
      ["sent.payloadSha256", metaState.sentPayloadSha256 ?? "—"],
    ]);
  }

  renderMetaState();

  if (Array.isArray(contract.roles) && !contract.roles.includes(result.role)) {
    setError("unknown_role", `role ${result.role} is not in contract roles`);
    return;
  }

  if (!serverUrl) {
    renderStatus("running", "waiting for server address (?ws=ws://host:port/path)");
    return;
  }

  renderStatus("running", "connecting …");

  let socket;
  try {
    socket = new WebSocket(serverUrl, subprotocol);
  } catch (error) {
    setError("bad_envelope", `WebSocket ctor failed: ${error.message}`);
    return;
  }

  let lastRevision = null;
  let commandSent = false;

  function send(message) {
    try {
      socket.send(JSON.stringify(message));
    } catch (error) {
      setError("bad_envelope", `send failed: ${error.message}`);
    }
  }

  async function sendHelloCommand() {
    if (commandSent) {
      return;
    }
    commandSent = true;
    const payload = "Hello World";
    let payloadSha256;
    try {
      payloadSha256 = await sha256Hex(payload);
    } catch (error) {
      setError("bad_payload_hash", `payload hash compute failed: ${error.message}`);
      return;
    }
    const sentAtMs = Date.now();
    send({
      messageType: "InputCommand",
      sender: result.role,
      sequence: 1,
      kind: constValue(contract, "InputCommand", "kind") ?? "hello",
      payload,
      payloadSha256,
      sentAtMs,
    });
    result.sent = { sequence: 1, payloadSha256, sentAtMs };
    metaState.sentSequence = 1;
    metaState.sentPayloadSha256 = payloadSha256;
    renderMetaState();
  }

  async function handleDelta(delta) {
    let recomputed;
    try {
      recomputed = await sha256Hex(delta.payload);
    } catch (error) {
      setError("bad_payload_hash", `delta hash recompute failed: ${error.message}`);
      return;
    }
    if (recomputed !== delta.payloadSha256) {
      setError("bad_payload_hash", `delta ${delta.sender}/${delta.sequence} hash mismatch`);
      return;
    }
    if (lastRevision !== null && delta.revision <= lastRevision) {
      setError("stale_revision", `delta revision ${delta.revision} is not > ${lastRevision}`);
      return;
    }
    lastRevision = delta.revision;
    const latencyMs = Date.now() - delta.originSentAtMs;
    result.received.push({
      sender: delta.sender,
      sequence: delta.sequence,
      tickId: delta.tickId,
      revision: delta.revision,
      payloadSha256: delta.payloadSha256,
      latencyMs,
    });
    if (result.status !== "error") {
      result.status = "ok";
      renderStatus(result.status, `${result.received.length} delta(s) received`);
    }
    dom.deltasEmpty?.remove();
    const row = document.createElement("tr");
    for (const cell of [delta.sender, delta.sequence, delta.tickId, delta.revision, delta.payloadSha256, latencyMs]) {
      const td = document.createElement("td");
      td.textContent = String(cell);
      row.append(td);
    }
    // Newest first: prepend so the live view shows the latest delta on top, and cap
    // the table so an unbounded stream cannot grow the DOM without limit.
    dom.deltas.prepend(row);
    while (dom.deltas.rows.length > MAX_RENDERED_DELTAS) {
      dom.deltas.deleteRow(-1);
    }
  }

  async function handleMessage(text) {
    let message;
    try {
      message = JSON.parse(text);
    } catch (error) {
      setError("bad_envelope", `invalid JSON frame: ${error.message}`);
      return;
    }
    if (typeof message !== "object" || message === null || typeof message.messageType !== "string") {
      setError("bad_envelope", "frame lacks a string messageType");
      return;
    }

    const messageType = message.messageType;
    const problems = validate(messageType, message);
    if (problems.length > 0) {
      const unknownMapping = problems.some((problem) => problem.problem.includes("unknown_mapping"));
      setError(
        unknownMapping ? "unknown_mapping" : "bad_envelope",
        problems.map((problem) => `${problem.field}: ${problem.problem}`).join("; "),
      );
      return;
    }

    switch (messageType) {
      case "HandshakeAck": {
        if (message.accepted !== true) {
          setError("handshake_rejected", message.reason ?? "handshake not accepted");
          return;
        }
        if (message.contractId !== contract.contractId) {
          setError("unsupported_contract", `server contractId ${message.contractId} != ${contract.contractId}`);
          return;
        }
        result.sessionId = message.sessionId;
        metaState.sessionId = message.sessionId;
        renderMetaState();
        renderStatus("running", "handshake accepted, waiting for baseline …");
        break;
      }
      case "FullSnapshot": {
        result.sessionId ??= message.sessionId;
        result.baselineRevision = message.revision;
        metaState.sessionId ??= message.sessionId;
        metaState.baselineRevision = message.revision;
        lastRevision = message.revision;
        send({ messageType: "BaselineAck", revision: message.revision });
        renderStatus("running", "baseline acknowledged, sending hello command …");
        await sendHelloCommand();
        break;
      }
      case "Delta": {
        await handleDelta(message);
        break;
      }
      case "Error": {
        setError(message.code, message.detail);
        break;
      }
      default: {
        // 契约在册但方向为 c2s 的消息(如 Shutdown)由 server 发来即协议违例。
        setError("unknown_mapping", `server sent client-direction message ${messageType}`);
        break;
      }
    }
  }

  socket.onopen = () => {
    const handshake = {
      messageType: "Handshake",
      role: result.role,
      clientName: "lumio-browser",
      contractId: contract.contractId,
    };
    if (launchCredential) handshake.admissionCredential = launchCredential;
    send(handshake);
    renderStatus("running", "connected, handshaking …");
  };
  socket.onmessage = (event) => {
    handleMessage(String(event.data)).catch((error) => setError("bad_envelope", String(error)));
  };
  // onerror 不额外打印:浏览器自身已记录连接错误,页面在 onclose 统一呈现终态。
  socket.onclose = () => {
    if (result.received.length > 0 && result.status !== "error") {
      result.status = "ok";
    }
    renderStatus(result.status, `connection closed (${result.received.length} delta(s) received)`);
  };
}

main().catch((error) => {
  // 兜底:任何未预见异常都进证据,而不是散落成未捕获错误。
  setError("bad_envelope", `unhandled: ${error?.message ?? error}`);
});
