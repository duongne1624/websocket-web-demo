let ws;

// Điền thông tin phòng để vào phòng và nhấn nút 'Join'
document.getElementById('submitButton').addEventListener('click', () => {
    const name = document.getElementById('userName').value;
    const room = document.getElementById('roomName').value;

    if (name && room) {
        // Sử dụng project Message-server để tạo websocket
        ws = new WebSocket(`wss://localhost:6969/ws?name=${name}&room=${room}`);

        ws.onopen = () => {
            document.getElementById('inputArea').style.display = 'flex';
            document.getElementById('userName').style.display = 'none';
            document.getElementById('roomName').style.display = 'none';
            document.getElementById('submitButton').style.display = 'none';
        };

        ws.onmessage = (event) => {
            const chatMessages = document.getElementById('chatMessages');
            const message = event.data;

            try {
                const messageData = JSON.parse(message);
                const timestamp = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

                const messageElement = document.createElement('div');
                messageElement.className = `chat-message ${messageData.sender === "System" ? 'system-message' : (messageData.sender === name ? 'me' : 'other')}`;

                if (messageData.sender === "System") {
                    // System message
                    messageElement.innerHTML = `<span class="system-text">${messageData.message}</span><span class="timestamp">${timestamp}</span>`;
                } else {
                    // Users message
                    messageElement.innerHTML = `<strong>${messageData.sender}:</strong> ${messageData.message}`;
                }

                chatMessages.appendChild(messageElement);
            } catch (error) {
                console.error('Error parsing message:', error);
            }

            chatMessages.scrollTop = chatMessages.scrollHeight;
        };

        ws.onclose = () => {
            console.log('WebSocket connection closed');
        };
    }
});

// Sự kiện khi nhấn nút 'Send'
document.getElementById('sendButton').addEventListener('click', () => {
    const messageInput = document.getElementById('messageInput');
    const message = messageInput.value;

    if (message) {
        const formattedMessage = JSON.stringify({ sender: document.getElementById('userName').value, message: message });
        console.log(formattedMessage);
        ws.send(formattedMessage);
        messageInput.value = '';
    }
});
