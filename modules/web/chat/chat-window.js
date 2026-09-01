// Room chat presentation for the Browser consumer (R-00349).
// Chat-window contents are client-only presentation state: they are not ReplicaWorld
// authority and are never restored from FullSnapshot (C-1 chat.event is delta-live-only).
// Wire field truth remains architecture origin/main gameplay-command-envelope-v1.json;
// this module does not extend hello-wire-v1.

export function createChatWindow() {
  return { lines: [] };
}

export function applyFullSnapshot(window) {
  window.lines = [];
}

export function appendAcceptedEvent(window, event) {
  if (!isEvent(event)) {
    return false;
  }

  const last = window.lines.length === 0 ? null : window.lines[window.lines.length - 1];
  if (last !== null && (event.roomSequence <= last.roomSequence || event.messageId <= last.messageId)) {
    return false;
  }

  window.lines = window.lines.concat([
    Object.freeze({
      messageId: event.messageId,
      roomSequence: event.roomSequence,
      senderNetEntityId: String(event.senderNetEntityId),
      text: event.text,
      appliedTick: event.appliedTick,
    }),
  ]);
  return true;
}

export function formatLine(event) {
  return `${event.senderNetEntityId}: ${event.text}`;
}

function isEvent(event) {
  return event !== null
    && typeof event === "object"
    && typeof event.messageId === "number"
    && typeof event.roomSequence === "number"
    && event.senderNetEntityId !== undefined
    && typeof event.text === "string"
    && typeof event.appliedTick === "number";
}
