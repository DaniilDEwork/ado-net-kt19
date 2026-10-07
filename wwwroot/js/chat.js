const messages = document.getElementById("chat-messages");
const input = document.getElementById("message");
const form = document.getElementById("message-form");
const sendButton = document.getElementById("send-button");
const reconnectButton = document.getElementById("reconnect-button");
const status = document.getElementById("connection-status");
const errorText = document.getElementById("message-error");
let sending = false;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .withAutomaticReconnect()
    .build();

function showError(message) {
    errorText.textContent = message;
    errorText.hidden = !message;
}

function updateStatus(text) {
    status.textContent = text;
    sendButton.disabled = sending || connection.state !== signalR.HubConnectionState.Connected;
}

connection.on("ReceiveMessage", (userName, message, sentAt) => {
    document.getElementById("chat-empty")?.remove();
    const item = document.createElement("div");
    item.className = "chat-message";
    const header = document.createElement("div");
    header.className = "chat-message-header";
    const user = document.createElement("strong");
    user.textContent = userName;
    const time = document.createElement("time");
    time.dateTime = sentAt;
    time.textContent = new Date(sentAt).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" });
    const text = document.createElement("p");
    text.textContent = message;
    header.append(user, time);
    item.append(header, text);
    messages.append(item);
    if (messages.children.length > 100) messages.firstElementChild.remove();
    messages.scrollTop = messages.scrollHeight;
});

connection.onreconnecting(() => {
    updateStatus("Соединение прервано, переподключение");
});

connection.onreconnected(() => {
    updateStatus("Подключено");
    reconnectButton.hidden = true;
    showError("");
});

connection.onclose(() => {
    updateStatus("Соединение закрыто");
    reconnectButton.hidden = false;
});

async function start() {
    reconnectButton.disabled = true;
    updateStatus("Подключение к чату");
    try {
        await connection.start();
        updateStatus("Подключено");
        reconnectButton.hidden = true;
        showError("");
    } catch {
        updateStatus("Не удалось подключиться к чату");
        showError("Проверьте подключение или войдите в аккаунт снова");
        reconnectButton.hidden = false;
    } finally {
        reconnectButton.disabled = false;
    }
}

form.addEventListener("submit", async event => {
    event.preventDefault();
    const message = input.value.trim();
    if (!message) {
        showError("Введите сообщение");
        return;
    }
    if (message.length > 500) {
        showError("Сообщение не должно быть длиннее 500 символов");
        return;
    }
    if (sending || connection.state !== signalR.HubConnectionState.Connected) return;
    sending = true;
    sendButton.disabled = true;
    showError("");
    try {
        await connection.invoke("SendMessage", message);
        input.value = "";
        input.focus();
    } catch {
        showError("Не удалось отправить сообщение, проверьте подключение или войдите снова");
    } finally {
        sending = false;
        sendButton.disabled = connection.state !== signalR.HubConnectionState.Connected;
    }
});

reconnectButton.addEventListener("click", start);
document.querySelector("form[data-logout]")?.addEventListener("submit", () => connection.stop());
start();
