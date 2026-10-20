// PasteFlow: reads the recently viewed tabs when the PasteFlow application asks for them.
// The application asks only when the user presses its shortcut. Nothing is read in the background, and the text goes
// only to PasteFlow, on this computer, through the native messaging of the browser.

const HOST = "com.pasteflow.host";
const MAX_TABS = 5;
const MAX_TEXT = 100000;
const MAX_LINKS = 300;
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

// Runs inside the page: the visible text and the links, never the values typed in fields
function readPage(maxText, maxLinks) {
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
  return { text, links };
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
      const [injection] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: readPage, args: [MAX_TEXT, MAX_LINKS] });
      const page = injection && injection.result ? injection.result : { text: "", links: [] };
      return { title: tab.title || "", url: tab.url, text: page.text, links: page.links, lastAccessed: tab.lastAccessed || 0 };
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
