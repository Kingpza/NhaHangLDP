/**
 * LDP Restaurant Chatbot - Enhanced Version
 * Features: AI Integration, Session Management, Voice Input, Rich UI
 */

(function ($) {
    'use strict';

    // ===== CONFIGURATION =====
    const CONFIG = {
        apiEndpoint: '/Public/ChatBotAI',
        searchEndpoint: '/Public/SearchMenu',
        suggestionsEndpoint: '/Public/GetQuickSuggestions',
        typingDelay: 500,
        messageDelay: 300,
        maxHistoryLength: 50,
        voiceEnabled: true
    };

    // ===== STATE =====
    let state = {
        isTyping: false,
        sessionId: null,
        isOpen: false,
        messageHistory: [],
        voiceRecognition: null
    };

    // ===== INITIALIZATION =====
    $(document).ready(function () {
        initChatbot();
        loadQuickSuggestions();
        initVoiceRecognition();
    });

    function initChatbot() {
        // Load session from localStorage
        state.sessionId = localStorage.getItem('chatbot_session_id');

        // Toggle chatbot
        $('.chatbot-toggle').click(function () {
            toggleChatbot();
        });

        // Close chatbot
        $('.chatbot-close').click(function () {
            closeChatbot();
        });

        // Send message
        $('.chatbot-send').click(function () {
            sendMessage();
        });

        // Enter key to send
        $('#chatbot-input').keypress(function (e) {
            if (e.which === 13 && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });

        // Voice input button
        if (CONFIG.voiceEnabled) {
            addVoiceButton();
        }

        // Show welcome message if first time
        if (!state.sessionId) {
            setTimeout(function () {
                showWelcomeMessage();
            }, 1000);
        }

        // Update badge
        updateBadge();
    }

    // ===== CHATBOT TOGGLE =====
    function toggleChatbot() {
        if (state.isOpen) {
            closeChatbot();
        } else {
            openChatbot();
        }
    }

    function openChatbot() {
        $('.chatbot-container').addClass('active');
        $('.chatbot-toggle').addClass('hidden');
        state.isOpen = true;
        $('#chatbot-input').focus();
        updateBadge(0);
        scrollToBottom();
    }

    function closeChatbot() {
        $('.chatbot-container').removeClass('active');
        $('.chatbot-toggle').removeClass('hidden');
        state.isOpen = false;
    }

    // ===== SEND MESSAGE =====
    function sendMessage() {
        const input = $('#chatbot-input');
        const message = input.val().trim();

        if (!message || state.isTyping) return;

        // Add user message
        addUserMessage(message);
        input.val('');

        // Show typing indicator
        showTypingIndicator();
        state.isTyping = true;

        // Disable input
        input.prop('disabled', true);
        $('.chatbot-send').prop('disabled', true);

        // Call API
        $.ajax({
            url: CONFIG.apiEndpoint,
            method: 'POST',
            data: {
                message: message,
                sessionId: state.sessionId
            },
            success: function (response) {
                hideTypingIndicator();

                if (response.success) {
                    // Save session ID
                    if (response.sessionId) {
                        state.sessionId = response.sessionId;
                        localStorage.setItem('chatbot_session_id', response.sessionId);
                    }

                    // Add bot response
                    setTimeout(function () {
                        addBotMessage(response.response);

                        // Add suggestions if any
                        if (response.suggestions && response.suggestions.length > 0) {
                            addSuggestions(response.suggestions);
                        }

                        // Add quick replies if any
                        if (response.quickReplies && response.quickReplies.length > 0) {
                            addQuickReplies(response.quickReplies);
                        }
                    }, CONFIG.messageDelay);
                } else {
                    addBotMessage(response.response || 'Xin lỗi, có lỗi xảy ra. Vui lòng thử lại!');
                }
            },
            error: function () {
                hideTypingIndicator();
                addBotMessage('❌ Không thể kết nối. Vui lòng kiểm tra mạng và thử lại!');
            },
            complete: function () {
                state.isTyping = false;
                input.prop('disabled', false);
                $('.chatbot-send').prop('disabled', false);
                input.focus();
            }
        });
    }

    // ===== MESSAGE DISPLAY =====
    function addUserMessage(text) {
        const time = getCurrentTime();
        const html = `
            <div class="message user" style="opacity: 0; transform: translateX(20px);">
                <div class="message-content">
                    <div class="message-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        
        // Animate in
        $('.message.user').last().animate({
            opacity: 1
        }, 300).css('transform', 'translateX(0)');
        
        scrollToBottom();

        // Save to history
        state.messageHistory.push({ role: 'user', content: text, time: time });
    }

    function addBotMessage(text) {
        const time = getCurrentTime();
        const formattedText = formatBotResponse(text);
        const html = `
            <div class="message bot" style="opacity: 0; transform: translateX(-20px);">
                <div class="message-content">
                    <div class="message-bubble">${formattedText}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        
        // Animate in
        $('.message.bot').last().animate({
            opacity: 1
        }, 300).css('transform', 'translateX(0)');
        
        scrollToBottom();

        // Save to history
        state.messageHistory.push({ role: 'bot', content: text, time: time });
    }

    function addSuggestions(suggestions) {
        if (!suggestions || suggestions.length === 0) return;

        let cardsHtml = suggestions.map(function (item) {
            // Lấy imageUrl từ cả 2 format (camelCase và PascalCase)
            let rawImageUrl = item.ImageUrl || item.imageUrl || '';
            
            // Xử lý đường dẫn hình ảnh
            let imageUrl = '/images/menu/default-food.jpg'; // Default image
            
            if (rawImageUrl) {
                // Nếu đã có đường dẫn đầy đủ (bắt đầu với / hoặc http)
                if (rawImageUrl.startsWith('/') || rawImageUrl.startsWith('http')) {
                    imageUrl = rawImageUrl;
                } else {
                    // Nếu chỉ là tên file, thêm đường dẫn đầy đủ
                    imageUrl = '/images/menu/' + rawImageUrl;
                }
            }
            
            const price = formatCurrency(item.Price || item.price || 0);
            const name = item.Name || item.name || 'Món ăn';
            const id = item.Id || item.id || 0;

            return `
                <div class="suggestion-card" data-id="${id}">
                    <img src="${imageUrl}" 
                         alt="${escapeHtml(name)}" 
                         onerror="this.src='/images/menu/default-food.jpg'">
                    <div class="suggestion-info">
                        <h4>${escapeHtml(name)}</h4>
                        <p class="suggestion-price">${price}</p>
                    </div>
                    <button class="btn-view-dish" data-id="${id}">
                        <i class="fas fa-eye"></i>
                    </button>
                </div>
            `;
        }).join('');

        const html = `
            <div class="message bot suggestions-container" style="opacity: 0;">
                <div class="suggestions-scroll">
                    ${cardsHtml}
                </div>
            </div>
        `;

        $('.chatbot-messages').append(html);
        $('.suggestions-container').last().animate({ opacity: 1 }, 300);
        scrollToBottom();

        // Bind click events
        $('.btn-view-dish').off('click').on('click', function (e) {
            e.stopPropagation();
            const id = $(this).data('id');
            window.open('/Public/Detail/' + id, '_blank');
        });

        $('.suggestion-card').off('click').on('click', function (e) {
            if (!$(e.target).hasClass('btn-view-dish') && !$(e.target).closest('.btn-view-dish').length) {
                const name = $(this).find('h4').text();
                $('#chatbot-input').val('Cho tôi biết thêm về ' + name);
                sendMessage();
            }
        });
    }

    function addQuickReplies(replies) {
        if (!replies || replies.length === 0) return;

        // Remove old quick replies
        $('.quick-replies-container').remove();

        const buttonsHtml = replies.map(function (reply) {
            return `<button class="quick-reply-btn">${reply}</button>`;
        }).join('');

        const html = `
            <div class="quick-replies-container" style="opacity: 0;">
                ${buttonsHtml}
            </div>
        `;

        $('.chatbot-messages').append(html);
        $('.quick-replies-container').last().animate({ opacity: 1 }, 300);
        scrollToBottom();

        // Bind click events
        $('.quick-reply-btn').off('click').on('click', function () {
            const text = $(this).text();
            $('#chatbot-input').val(text);
            $(this).closest('.quick-replies-container').remove();
            sendMessage();
        });
    }

    // ===== TYPING INDICATOR =====
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
        $('.typing-message').fadeOut(200, function () {
            $(this).remove();
        });
    }

    // ===== WELCOME MESSAGE =====
    function showWelcomeMessage() {
        const hour = new Date().getHours();
        let greeting;

        if (hour < 10) greeting = 'Chào buổi sáng';
        else if (hour < 14) greeting = 'Chào buổi trưa';
        else if (hour < 18) greeting = 'Chào buổi chiều';
        else greeting = 'Chào buổi tối';

        const welcomeText = `👋 ${greeting}! Mình là **LDP Bot**.\n\nMình có thể giúp bạn tìm món ăn, gợi ý theo ngân sách, hoặc hỗ trợ đặt bàn.\n\nBạn muốn tìm gì hôm nay?`;
        
        addBotMessage(welcomeText);
        
        addQuickReplies([
            '🔥 Top món bán chạy',
            '💰 Món dưới 100k',
            '⭐ Món đặc biệt',
            '📅 Đặt bàn'
        ]);
    }

    // ===== QUICK SUGGESTIONS =====
    function loadQuickSuggestions() {
        $.get(CONFIG.suggestionsEndpoint, function (response) {
            if (response.success) {
                // Could update UI with time-based suggestions
                console.log('Quick suggestions loaded:', response.mealType);
            }
        });
    }

    // ===== VOICE RECOGNITION =====
    function initVoiceRecognition() {
        if (!('webkitSpeechRecognition' in window) && !('SpeechRecognition' in window)) {
            CONFIG.voiceEnabled = false;
            return;
        }

        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        state.voiceRecognition = new SpeechRecognition();
        state.voiceRecognition.continuous = false;
        state.voiceRecognition.interimResults = false;
        state.voiceRecognition.lang = 'vi-VN';

        state.voiceRecognition.onresult = function (event) {
            const transcript = event.results[0][0].transcript;
            $('#chatbot-input').val(transcript);
            stopVoiceRecording();
            sendMessage();
        };

        state.voiceRecognition.onerror = function (event) {
            console.error('Voice recognition error:', event.error);
            stopVoiceRecording();
        };

        state.voiceRecognition.onend = function () {
            stopVoiceRecording();
        };
    }

    function addVoiceButton() {
        const voiceBtn = `
            <button class="chatbot-voice" title="Nhập bằng giọng nói">
                <i class="fas fa-microphone"></i>
            </button>
        `;
        $('.chatbot-input').append(voiceBtn);

        $('.chatbot-voice').click(function () {
            toggleVoiceRecording();
        });
    }

    function toggleVoiceRecording() {
        if (!state.voiceRecognition) return;

        const btn = $('.chatbot-voice');
        
        if (btn.hasClass('recording')) {
            state.voiceRecognition.stop();
            stopVoiceRecording();
        } else {
            state.voiceRecognition.start();
            btn.addClass('recording');
            btn.find('i').removeClass('fa-microphone').addClass('fa-stop');
        }
    }

    function stopVoiceRecording() {
        const btn = $('.chatbot-voice');
        btn.removeClass('recording');
        btn.find('i').removeClass('fa-stop').addClass('fa-microphone');
    }

    // ===== UTILITIES =====
    function formatBotResponse(text) {
        if (!text) return '';

        return text
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')  // Bold
            .replace(/\*(.*?)\*/g, '<em>$1</em>')              // Italic
            .replace(/\n/g, '<br>');                            // Line breaks
    }

    function formatCurrency(amount) {
        if (!amount) return '0đ';
        return new Intl.NumberFormat('vi-VN').format(amount) + 'đ';
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
        return text.replace(/[&<>"']/g, function (m) { return map[m]; });
    }

    function getCurrentTime() {
        const now = new Date();
        return now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }

    function scrollToBottom() {
        const container = $('.chatbot-messages');
        container.animate({
            scrollTop: container[0].scrollHeight
        }, 300);
    }

    function updateBadge(count) {
        const badge = $('.chatbot-badge');
        if (count === 0 || count === undefined) {
            badge.hide();
        } else {
            badge.text(count).show();
        }
    }

})(jQuery);
