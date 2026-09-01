import test from "node:test";
import assert from "node:assert/strict";
import { appendAcceptedEvent, applyFullSnapshot, createChatWindow, formatLine } from "./chat-window.js";

test("browser window shows bot sender NetEntityId and text", () => {
  const window = createChatWindow();
  assert.equal(
    appendAcceptedEvent(window, {
      messageId: 1,
      roomSequence: 1,
      senderNetEntityId: "101",
      text: "gg",
      appliedTick: 7,
    }),
    true,
  );
  assert.equal(window.lines.length, 1);
  assert.equal(window.lines[0].senderNetEntityId, "101");
  assert.equal(window.lines[0].text, "gg");
  assert.equal(formatLine(window.lines[0]), "101: gg");
});

test("two windows receive identical MessageId/roomSequence without shared refs", () => {
  const browser = createChatWindow();
  const bot = createChatWindow();
  const first = { messageId: 1, roomSequence: 1, senderNetEntityId: "101", text: "gg", appliedTick: 7 };
  const second = { messageId: 2, roomSequence: 2, senderNetEntityId: "101", text: "hi", appliedTick: 8 };
  assert.equal(appendAcceptedEvent(browser, first), true);
  assert.equal(appendAcceptedEvent(bot, { ...first }), true);
  assert.equal(appendAcceptedEvent(browser, second), true);
  assert.equal(appendAcceptedEvent(bot, { ...second }), true);
  assert.notEqual(browser, bot);
  assert.notEqual(browser.lines, bot.lines);
  assert.deepEqual(
    browser.lines.map((line) => [line.messageId, line.roomSequence]),
    bot.lines.map((line) => [line.messageId, line.roomSequence]),
  );
});

test("malformed or regressing events do not mutate the window", () => {
  const window = createChatWindow();
  appendAcceptedEvent(window, {
    messageId: 1,
    roomSequence: 1,
    senderNetEntityId: "101",
    text: "gg",
    appliedTick: 7,
  });
  const before = window.lines;
  assert.equal(appendAcceptedEvent(window, null), false);
  assert.equal(
    appendAcceptedEvent(window, {
      messageId: 1,
      roomSequence: 1,
      senderNetEntityId: "101",
      text: "dup",
      appliedTick: 8,
    }),
    false,
  );
  applyFullSnapshot(window);
  assert.equal(window.lines.length, 0);
  assert.notEqual(window.lines, before);
});
