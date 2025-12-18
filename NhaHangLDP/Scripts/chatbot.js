// ===== CHATBOT JAVASCRIPT =====

$(document).ready(function () {
    let isOpen = false;
    let isTyping = false;

    // Khởi tạo chatbot
    function initChatbot() {
        // Thêm welcome message
        setTimeout(() => {
            addBotMessage(
                "Xin chào! 👋 Tôi là trợ lý ảo của Nhà Hàng LDP. Tôi có thể giúp bạn tìm món ăn phù hợp!\n\n" +
                "Bạn có thể hỏi tôi về:\n" +
                "• Giá món ăn\n" +
                "• Combo/Set\n" +
                "• Loại món\n" +
                "• Gợi ý món"
            );
            addQuickReplies([
                "Gợi ý món cho tôi",
                "Món dưới 100k",
                "Có combo nào?",
                "Món chính"
            ]);
        }, 500);
    }

    // Toggle chatbot
    $('.chatbot-toggle').click(function () {
        isOpen = !isOpen;
        if (isOpen) {
            $('.chatbot-container').addClass('active');
            if ($('.chatbot-messages .message').length === 0) {
                initChatbot();
            }
        } else {
            $('.chatbot-container').removeClass('active');
        }
    });

    // Đóng chatbot
    $('.chatbot-close').click(function () {
        isOpen = false;
        $('.chatbot-container').removeClass('active');
    });

    // Gửi tin nhắn
    function sendMessage() {
        const message = $('#chatbot-input').val().trim();
        if (!message || isTyping) return;

        // Hiển thị tin nhắn của user
        addUserMessage(message);
        $('#chatbot-input').val('');

        // Disable input khi đang xử lý
        isTyping = true;
        $('#chatbot-input, .chatbot-send').prop('disabled', true);

        // Hiển thị typing indicator
        showTypingIndicator();

        // Gọi API
        $.ajax({
            url: '/Public/ChatBot',
            type: 'POST',
            data: { message: message },
            success: function (response) {
                hideTypingIndicator();
                
                if (response.success) {
                    // Hiển thị response
                    addBotMessage(response.response);

                    // Hiển thị suggestions nếu có
                    if (response.suggestions && response.suggestions.length > 0) {
                        addSuggestions(response.suggestions);
                    }
                } else {
                    addBotMessage("Xin lỗi, có lỗi xảy ra. Vui lòng thử lại sau!");
                }
            },
            error: function () {
                hideTypingIndicator();
                addBotMessage("Xin lỗi, không thể kết nối đến máy chủ. Vui lòng thử lại sau!");
            },
            complete: function () {
                isTyping = false;
                $('#chatbot-input, .chatbot-send').prop('disabled', false);
                $('#chatbot-input').focus();
            }
        });
    }

    // Xử lý sự kiện gửi tin nhắn
    $('.chatbot-send').click(sendMessage);

    $('#chatbot-input').keypress(function (e) {
        if (e.which === 13 && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    // Thêm tin nhắn của user
    function addUserMessage(text) {
        const time = getCurrentTime();
        const html = `
            <div class="message user">
                <div class="message-content">
                    <div class="message-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${time}</div>
                </div>
                <div class="message-avatar">
                    <i class="fas fa-user"></i>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        scrollToBottom();
    }

    // Thêm tin nhắn của bot
    function addBotMessage(text) {
        const time = getCurrentTime();
        const formattedText = text.replace(/\n/g, '<br>');
        const html = `
            <div class="message bot">
                <div class="message-avatar">
                    <i class="fas fa-robot"></i>
                </div>
                <div class="message-content">
                    <div class="message-bubble">${formattedText}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        scrollToBottom();
    }

    // Hiển thị typing indicator
    function showTypingIndicator() {
        const html = `
            <div class="message bot typing-message">
                <div class="message-avatar">
                    <i class="fas fa-robot"></i>
                </div>
                <div class="message-content">
                    <div class="typing-indicator">
                        <div class="typing-dot"></div>
                        <div class="typing-dot"></div>
                        <div class="typing-dot"></div>
                    </div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        scrollToBottom();
    }

    // Ẩn typing indicator
    function hideTypingIndicator() {
        $('.typing-message').remove();
    }

    // Thêm suggestions (món ăn)
    function addSuggestions(suggestions) {
        let html = '<div class="suggestions">';
        
        suggestions.forEach(item => {
            const imageUrl = item.imageUrl 
                ? `/images/menu/${item.imageUrl}` 
                : '/images/menu/default-food.jpg';
            
            const price = item.price 
                ? formatPrice(item.price) 
                : formatPrice(item.price || 0);
            
            const description = item.description 
                ? `<div class="suggestion-description">${escapeHtml(item.description)}</div>` 
                : '';

            const linkUrl = item.isCombo 
                ? '#' 
                : `/Public/Detail/${item.id}`;

            html += `
                <a href="${linkUrl}" target="_blank" class="suggestion-card" data-id="${item.id}">
                    <img src="${imageUrl}" alt="${escapeHtml(item.name)}" class="suggestion-image" onerror="this.src='/images/menu/default-food.jpg'">
                    <div class="suggestion-info">
                        <div class="suggestion-name">${escapeHtml(item.name)}</div>
                        <div class="suggestion-price">${price}</div>
                        ${description}
                    </div>
                </a>
            `;
        });
        
        html += '</div>';
        $('.chatbot-messages').append(html);
        scrollToBottom();
    }

    // Thêm quick replies
    function addQuickReplies(replies) {
        let html = '<div class="quick-replies">';
        
        replies.forEach(reply => {
            html += `<button class="quick-reply" data-text="${escapeHtml(reply)}">${escapeHtml(reply)}</button>`;
        });
        
        html += '</div>';
        $('.chatbot-messages').append(html);
        scrollToBottom();

        // Xử lý click vào quick reply
        $('.quick-reply').click(function () {
            const text = $(this).data('text');
            $(this).parent().remove(); // Xóa quick replies sau khi click
            $('#chatbot-input').val(text);
            sendMessage();
        });
    }

    // Helper functions
    function getCurrentTime() {
        const now = new Date();
        const hours = String(now.getHours()).padStart(2, '0');
        const minutes = String(now.getMinutes()).padStart(2, '0');
        return `${hours}:${minutes}`;
    }

    function scrollToBottom() {
        const messagesContainer = $('.chatbot-messages');
        messagesContainer.animate({
            scrollTop: messagesContainer[0].scrollHeight
        }, 300);
    }

    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, m => map[m]);
    }

    function formatPrice(price) {
        return new Intl.NumberFormat('vi-VN', {
            style: 'currency',
            currency: 'VND'
        }).format(price);
    }

    // Auto-focus input khi mở chatbot
    $('.chatbot-toggle').click(function () {
        setTimeout(() => {
            if (isOpen) {
                $('#chatbot-input').focus();
            }
        }, 300);
    });

    // Hiệu ứng badge (thông báo)
    setTimeout(() => {
        if (!isOpen) {
            // Có thể thêm logic hiển thị badge khi có tin nhắn mới
        }
    }, 5000);
});
