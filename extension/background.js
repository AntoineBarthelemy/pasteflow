// PasteFlow: reads the recently viewed tabs when the PasteFlow application asks for them.
// The application asks only when the user presses its shortcut. Nothing is read in the background, and the text goes
// only to PasteFlow, on this computer, through the native messaging of the browser.

const HOST = "com.pasteflow.host";
const MAX_TABS = 5;
const MAX_TEXT = 100000;
const MAX_LINKS = 300;
const MAX_FIELDS = 100;
const MAX_VALUE = 5000;
const RETRY_ALARM = "pasteflow-reconnect";

let port = null;

function connect() {
  if (port) {
    return;
  }
  try {
    port = chrome.runtime.connectNative(HOST);
  } catch (error) {
    port = null;
    return;
  }
  port.onMessage.addListener(onMessage);
  port.onDisconnect.addListener(() => {
    // PasteFlow is not installed or not registered yet: the alarm tries again
    void chrome.runtime.lastError;
    port = null;
  });
}

// Runs inside the page: the visible text, the links, and the fields the user filled with their label, so that a form
// can be copied into another. Never a password, a payment card or a hidden field.
function readPage(maxText, maxLinks, maxFields, maxValue) {
  const text = (document.body ? document.body.innerText : "").slice(0, maxText);
  const links = [];
  for (const anchor of document.querySelectorAll("a[href]")) {
    if (links.length >= maxLinks) {
      break;
    }
    const label = (anchor.innerText || anchor.getAttribute("aria-label") || "").trim();
    if (label && anchor.href) {
      links.push({ text: label.slice(0, 200), href: anchor.href });
    }
  }
  const fields = [];
  const labelOf = (element) => {
    const byId = element.id ? document.querySelector(`label[for="${CSS.escape(element.id)}"]`) : null;
    const labelledBy = (element.getAttribute("aria-labelledby") || "")
      .split(/\s+/)
      .map((id) => document.getElementById(id))
      .filter((node) => node)
      .map((node) => node.innerText)
      .join(" ");
    const around = element.closest("label");
    const text = (byId && byId.innerText) || element.getAttribute("aria-label") || labelledBy || (around && around.innerText)
      || element.getAttribute("placeholder") || element.getAttribute("name") || "";
    return text.trim().slice(0, 100);
  };
  for (const element of document.querySelectorAll("input, textarea, select")) {
    if (fields.length >= maxFields) {
      break;
    }
    const type = (element.getAttribute("type") || "").toLowerCase();
    const autocomplete = (element.getAttribute("autocomplete") || "").toLowerCase();
    const isSecret = ["password", "hidden", "file", "checkbox", "radio", "submit", "button", "image", "reset"].includes(type)
      || autocomplete.startsWith("cc-") || autocomplete.includes("password");
    const value = element.tagName === "SELECT"
      ? (element.selectedOptions[0] ? element.selectedOptions[0].text : "")
      : element.value;
    const label = labelOf(element);
    if (!isSecret && label && value && value.trim() && value !== element.getAttribute("placeholder")) {
      fields.push({ label, value: value.slice(0, maxValue) });
    }
  }
  return { text, links, fields };
}

function isReadable(tab) {
  return !tab.incognito && !tab.discarded && typeof tab.url === "string" && /^https?:/i.test(tab.url);
}

async function readTabs(max) {
  const tabs = (await chrome.tabs.query({})).filter(isReadable);
  tabs.sort((first, second) => (second.lastAccessed || 0) - (first.lastAccessed || 0));
  const recent = tabs.slice(0, Math.min(max || MAX_TABS, MAX_TABS));
  const read = await Promise.all(recent.map(async (tab) => {
    try {
      const [injection] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: readPage, args: [MAX_TEXT, MAX_LINKS, MAX_FIELDS, MAX_VALUE] });
      const page = injection && injection.result ? injection.result : { text: "", links: [], fields: [] };
      return { title: tab.title || "", url: tab.url, text: page.text, links: page.links, fields: page.fields, lastAccessed: tab.lastAccessed || 0 };
    } catch (error) {
      // A page the browser protects, such as its store or settings: left aside
      return null;
    }
  }));
  return read.filter((tab) => tab !== null);
}

async function onMessage(message) {
  if (!message || message.type !== "read" || !port) {
    return;
  }
  const tabs = await readTabs(message.max);
  if (port) {
    port.postMessage({ type: "tabs", id: message.id, tabs });
  }
}

async function ensureRetryAlarm() {
  if (!(await chrome.alarms.get(RETRY_ALARM))) {
    await chrome.alarms.create(RETRY_ALARM, { periodInMinutes: 1 });
  }
}

chrome.runtime.onStartup.addListener(connect);
chrome.runtime.onInstalled.addListener(connect);
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === RETRY_ALARM) {
    connect();
  }
});

void ensureRetryAlarm();
connect();
