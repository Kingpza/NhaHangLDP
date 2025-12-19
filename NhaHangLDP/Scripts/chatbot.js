// ===== MODERN AI CHATBOT WITH ENHANCED FEATURES =====

$(document).ready(function () {
    let isOpen = false;
    let isTyping = false;
    let messageHistory = [];

    // Khởi tạo chatbot với welcome message hiện đại
    function initChatbot() {
        setTimeout(() => {
            addBotMessage(
                "👋 Xin chào! Mình là **LDP Bot** - trợ lý ảo thông minh của Nhà Hàng LDP.\n\n" +
                "Mình có thể giúp bạn:\n" +
                "🍽️ Tìm món ăn theo sở thích\n" +
                "💰 Gợi ý món theo ngân sách\n" +
                "🎯 Tư vấn combo & set meal\n" +
                "⭐ Xem món bán chạy\n\n" +
                "Hãy thử hỏi mình nhé! 😊"
            );
            addQuickReplies([
                "🔥 Top món bán chạy",
                "💰 Món dưới 100k",
                "🍜 Món chính ngon",
                "🥗 Món khai vị"
            ]);
        }, 600);
    }

    // Toggle chatbot với animation
    $('.chatbot-toggle').click(function () {
        isOpen = !isOpen;
        if (isOpen) {
            $('.chatbot-container').addClass('active');
            $('.chatbot-badge').fadeOut(200);
            if ($('.chatbot-messages .message').length === 0) {
                initChatbot();
            }
            // Track analytics
            trackChatbotOpen();
        } else {
            $('.chatbot-container').removeClass('active');
        }
    });

    // Đóng chatbot
    $('.chatbot-close').click(function () {
        isOpen = false;
        $('.chatbot-container').removeClass('active');
    });

    // Gửi tin nhắn với enhanced features
    function sendMessage() {
        const message = $('#chatbot-input').val().trim();
        if (!message || isTyping) return;

        // Add to history
        messageHistory.push({ role: 'user', content: message });

        // Hiển thị tin nhắn của user
        addUserMessage(message);
        $('#chatbot-input').val('');

        // Disable input
        isTyping = true;
        $('#chatbot-input, .chatbot-send').prop('disabled', true);
        $('.chatbot-send').html('<i class="fas fa-spinner fa-spin"></i>');

        // Show typing
        showTypingIndicator();

        // Smart delay based on message length
        const baseDelay = 800;
        const delayPerChar = 10;
        const responseDelay = Math.min(baseDelay + (message.length * delayPerChar), 2000);

        // Call API
        setTimeout(() => {
            $.ajax({
                url: '/Public/ChatBot',
                type: 'POST',
                data: { 
                    message: message,
                    history: JSON.stringify(messageHistory.slice(-5)) // Last 5 messages for context
                },
                success: function (response) {
                    console.log('✅ Chatbot response:', response);
                    hideTypingIndicator();

                    if (response.success) {
                        // Add to history
                        messageHistory.push({ role: 'bot', content: response.response });

                        // Format and display response
                        const formattedResponse = formatBotResponse(response.response);
                        addBotMessage(formattedResponse);

                        // Display suggestions with animation
                        if (response.suggestions && response.suggestions.length > 0) {
                            setTimeout(() => {
                                addSuggestions(response.suggestions);
                            }, 400);
                        }

                        // Auto quick replies based on context
                        if (response.quickReplies && response.quickReplies.length > 0) {
                            setTimeout(() => {
                                addQuickReplies(response.quickReplies);
                            }, 600);
                        }
                    } else {
                        addBotMessage("😕 Xin lỗi, có lỗi xảy ra. Vui lòng thử lại sau!");
                    }
                },
                error: function (xhr, status, error) {
                    console.error('❌ Chatbot error:', error);
                    hideTypingIndicator();
                    addBotMessage(
                        "🔌 Không thể kết nối đến máy chủ.\n\n" +
                        "Vui lòng:\n" +
                        "• Kiểm tra kết nối internet\n" +
                        "• Thử lại sau vài giây\n" +
                        "• Liên hệ admin nếu lỗi tiếp diễn"
                    );
                },
                complete: function () {
                    isTyping = false;
                    $('#chatbot-input, .chatbot-send').prop('disabled', false);
                    $('.chatbot-send').html('<i class="fas fa-paper-plane"></i>');
                    $('#chatbot-input').focus();
                }
            });
        }, responseDelay);
    }

    // Event handlers
    $('.chatbot-send').click(sendMessage);

    $('#chatbot-input').keypress(function (e) {
        if (e.which === 13 && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    // Format bot response with markdown-like syntax
    function formatBotResponse(text) {
        if (!text) return '';
        
        return text
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>') // Bold
            .replace(/\*(.*?)\*/g, '<em>$1</em>') // Italic
            .replace(/\n/g, '<br>'); // Line breaks
    }

    // Add user message với animation (No Avatar)
    function addUserMessage(text) {
        const time = getCurrentTime();
        const html = `
            <div class="message user" style="opacity: 0;">
                <div class="message-content">
                    <div class="message-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        $('.message.user').last().animate({ opacity: 1 }, 300);
        scrollToBottom();
    }

    // Add bot message với rich formatting (No Avatar)
    function addBotMessage(text) {
        const time = getCurrentTime();
        const formattedText = formatBotResponse(text);
        const html = `
            <div class="message bot" style="opacity: 0;">
                <div class="message-content">
                    <div class="message-bubble">${formattedText}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        $('.message.bot').last().animate({ opacity: 1 }, 300);
        scrollToBottom();
    }

    // Typing indicator với animation (No Avatar)
    function showTypingIndicator() {
        const html = `
            <div class="message bot typing-message" style="opacity: 0;">
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
        $('.typing-message').animate({ opacity: 1 }, 200);
        scrollToBottom();
    }

    function hideTypingIndicator() {
        $('.typing-message').fadeOut(200, function() {
            $(this).remove();
        });
    }

    // Enhanced suggestions với lazy loading images
    function addSuggestions(suggestions) {
        if (!suggestions || suggestions.length === 0) return;

        console.log('📦 Adding', suggestions.length, 'suggestions');

        const container = $('<div class="suggestions" style="opacity: 0;"></div>');

        suggestions.forEach((item, index) => {
            // Xử lý đường dẫn hình ảnh
            let imageUrl = '/images/menu/default-food.jpg';
            if (item.imageUrl && item.imageUrl.trim() !== '') {
                imageUrl = '/images/menu/' + item.imageUrl;
            } else if (item.ImageUrl && item.ImageUrl.trim() !== '') {
                imageUrl = '/images/menu/' + item.ImageUrl;
            }

            const price = formatPrice(item.price || item.Price || 0);
            const description = item.description || item.Description || '';
            const descriptionHtml = description 
                ? `<div class="suggestion-description">${escapeHtml(truncateText(description, 80))}</div>`
                : '';
            
            const linkUrl = `/Public/Detail/${item.id || item.Id}`;

            const card = $(`
                <a href="${linkUrl}" target="_blank" class="suggestion-card" data-id="${item.id || item.Id}" style="opacity: 0; transform: translateY(20px);">
                    <img src="${imageUrl}" 
                         alt="${escapeHtml(item.name || item.Name)}" 
                         class="suggestion-image" 
                         loading="lazy"
                         onerror="this.src='/images/menu/default-food.jpg'">
                    <div class="suggestion-info">
                        <div class="suggestion-name">${escapeHtml(item.name || item.Name)}</div>
                        <div class="suggestion-price">${price}</div>
                        ${descriptionHtml}
                    </div>
                </a>
            `);

            container.append(card);

            // Animate each card with delay
            setTimeout(() => {
                card.animate({ 
                    opacity: 1, 
                    transform: 'translateY(0)' 
                }, 400);
            }, index * 100);
        });

        $('.chatbot-messages').append(container);
        container.animate({ opacity: 1 }, 300);

        setTimeout(() => scrollToBottom(), suggestions.length * 100 + 400);
    }

    // Quick replies với enhanced UX
    function addQuickReplies(replies) {
        const container = $('<div class="quick-replies" style="opacity: 0;"></div>');

        replies.forEach((reply, index) => {
            const button = $(`
                <button class="quick-reply" 
                        data-text="${escapeHtml(reply)}"
                        style="opacity: 0; transform: scale(0.8);">
                    ${escapeHtml(reply)}
                </button>
            `);

            container.append(button);

            // Animate each button
            setTimeout(() => {
                button.animate({ 
                    opacity: 1,
                    transform: 'scale(1)'
                }, 300);
            }, index * 80);
        });

        $('.chatbot-messages').append(container);
        container.animate({ opacity: 1 }, 200);
        scrollToBottom();

        // Event handler with animation
        $('.quick-reply').click(function () {
            const text = $(this).data('text');
            $(this).parent().fadeOut(300, function() {
                $(this).remove();
            });
            $('#chatbot-input').val(text);
            setTimeout(sendMessage, 100);
        });
    }

    // Helper functions
    function getCurrentTime() {
        const now = new Date();
        return `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
    }

    function scrollToBottom() {
        const container = $('.chatbot-messages');
        if (container.length > 0) {
            container.animate({
                scrollTop: container[0].scrollHeight
            }, 500, 'swing');
        }
    }

    function escapeHtml(text) {
        if (!text) return '';
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return String(text).replace(/[&<>"']/g, m => map[m]);
    }

    function formatPrice(price) {
        return new Intl.NumberFormat('vi-VN', {
            style: 'currency',
            currency: 'VND',
            minimumFractionDigits: 0
        }).format(price);
    }

    function truncateText(text, maxLength) {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength).trim() + '...';
    }

    function trackChatbotOpen() {
        console.log('📊 Chatbot opened at', new Date().toISOString());
        // Add analytics tracking here
    }

    // Auto-focus input
    $('.chatbot-toggle').click(function () {
        setTimeout(() => {
            if (isOpen) $('#chatbot-input').focus();
        }, 400);
    });

    // Show badge after 30 seconds
    setTimeout(() => {
        if (!isOpen) {
            $('.chatbot-badge').fadeIn(400);
        }
    }, 30000);

    // Close on ESC key
    $(document).keyup(function(e) {
        if (e.key === "Escape" && isOpen) {
            $('.chatbot-close').click();
        }
    });

    // Prevent closing when clicking inside
    $('.chatbot-container').click(function(e) {
        e.stopPropagation();
    });

    console.log('🤖 Chatbot initialized successfully');
});
