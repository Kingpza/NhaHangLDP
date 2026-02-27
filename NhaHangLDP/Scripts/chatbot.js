/**
 * LDP Restaurant Chatbot - Enhanced Version with Live Support
 * Features: AI Integration, Live Chat Support, SignalR Real-time, Session Management, Voice Input
 */

(function ($) {
    'use strict';

    // ===== CONFIGURATION =====
    const CONFIG = {
        apiEndpoint: '/Public/ChatBotAI',
        searchEndpoint: '/Public/SearchMenu',
        suggestionsEndpoint: '/Public/GetQuickSuggestions',
        addToCartEndpoint: '/Public/ChatBotAddToCart',
        getCartEndpoint: '/Public/ChatBotGetCart',
        clearCartEndpoint: '/Public/ChatBotClearCart',
        sendCustomerMessageEndpoint: '/ChatSupport/SendCustomerMessage',
        getChatHistoryEndpoint: '/ChatSupport/GetChatHistory',
        getUnreadCountEndpoint: '/ChatSupport/GetUnreadCount',
        markAsReadEndpoint: '/ChatSupport/MarkAsRead',
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
        voiceRecognition: null,
        mode: 'bot', // 'bot' or 'live'
        signalRConnected: false,
        customerId: null,
        customerName: 'Khách',
        unreadCount: 0
    };

    // ===== INITIALIZATION =====
    $(document).ready(function () {
        initChatbot();
        loadQuickSuggestions();
        initVoiceRecognition();
        initSignalR();
        checkUnreadMessages();
    });

    function initChatbot() {
        // Load session from localStorage
        state.sessionId = localStorage.getItem('chatbot_session_id');
        state.mode = localStorage.getItem('chatbot_mode') || 'bot';

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

        // Add mode toggle button
        addModeToggle();

        // Show welcome message if first time
        if (!state.sessionId) {
            setTimeout(function () {
                showWelcomeMessage();
            }, 1000);
        }

        // Update UI based on mode
        updateModeUI();

        // Update badge
        updateBadge();
    }

    // ===== SIGNALR INTEGRATION =====
    function initSignalR() {
        // Check if SignalR is available
        if (typeof $.connection === 'undefined' || !$.connection.restaurantHub) {
            console.log('SignalR not available, live chat will use polling');
            return;
        }

        var hub = $.connection.restaurantHub;

        // Receive support reply
        hub.client.receiveSupportReply = function (data) {
            if (state.mode === 'live') {
                addStaffMessage(data.staffName, data.message, data.timestamp);
                
                if (!state.isOpen) {
                    state.unreadCount++;
                    updateBadge(state.unreadCount);
                    showNotification('Tin nhắn mới từ hỗ trợ', data.message);
                }
            }
        };

        // User typing indicator
        hub.client.onUserTyping = function (data) {
            if (data.isTyping && state.mode === 'live') {
                showTypingIndicator();
            } else {
                hideTypingIndicator();
            }
        };

        // Start connection
        $.connection.hub.qs = { 
            role: 'Customer',
            userId: getCustomerId()
        };

        $.connection.hub.start().done(function () {
            state.signalRConnected = true;
            console.log('Chat SignalR connected');
            
            // Join customer group
            hub.server.joinGroup('user_' + getCustomerId());
        }).fail(function (error) {
            console.error('SignalR connection failed:', error);
        });
    }

    function getCustomerId() {
        if (state.customerId) return state.customerId;
        
        // Try to get from page data
        var customerId = $('#chatbot-customer-id').val() || 
                        $('body').data('customer-id') ||
                        localStorage.getItem('chatbot_guest_id');
        
        if (!customerId) {
            customerId = 'guest_' + Math.random().toString(36).substr(2, 9);
            localStorage.setItem('chatbot_guest_id', customerId);
        }
        
        state.customerId = customerId;
        return customerId;
    }

    // ===== MODE TOGGLE =====
    function addModeToggle() {
        const toggleHtml = `
            <div class="chat-mode-toggle">
                <button class="mode-btn ${state.mode === 'bot' ? 'active' : ''}" data-mode="bot" title="Chat với Bot AI">
                    <i class="fas fa-robot"></i>
                    <span>Bot AI</span>
                </button>
                <button class="mode-btn ${state.mode === 'live' ? 'active' : ''}" data-mode="live" title="Chat với nhân viên">
                    <i class="fas fa-headset"></i>
                    <span>Nhân viên</span>
                </button>
            </div>
        `;
        
        $('.chatbot-header').after(toggleHtml);
        
        $('.mode-btn').on('click', function() {
            const newMode = $(this).data('mode');
            switchMode(newMode);
        });
    }

    function switchMode(newMode) {
        if (newMode === state.mode) return;
        
        state.mode = newMode;
        localStorage.setItem('chatbot_mode', newMode);
        
        // Update UI
        $('.mode-btn').removeClass('active');
        $(`.mode-btn[data-mode="${newMode}"]`).addClass('active');
        
        updateModeUI();
        
        // Clear messages and show appropriate welcome
        $('.chatbot-messages').empty();
        
        if (newMode === 'live') {
            showLiveChatWelcome();
            loadChatHistory();
        } else {
            showWelcomeMessage();
        }
    }

    function updateModeUI() {
        const $container = $('.chatbot-container');
        const $header = $('.chatbot-header');
        const $title = $('.chatbot-title h4');
        const $status = $('.chatbot-status span:last');
        
        if (state.mode === 'live') {
            $container.addClass('live-mode').removeClass('bot-mode');
            $title.text('Hỗ trợ trực tuyến');
            $status.text(state.signalRConnected ? 'Đang kết nối' : 'Chờ nhân viên');
            $('#chatbot-input').attr('placeholder', 'Nhập tin nhắn cho nhân viên...');
        } else {
            $container.addClass('bot-mode').removeClass('live-mode');
            $title.text('Trợ lý ảo LDP');
            $status.text('Đang online');
            $('#chatbot-input').attr('placeholder', 'Nhập tin nhắn của bạn...');
        }
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
        
        // Mark messages as read
        if (state.mode === 'live') {
            markAsRead();
        }
        
        state.unreadCount = 0;
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

        // Handle special cart actions
        if (state.mode === 'bot' && (message === '❌ Xóa giỏ hàng' || message.toLowerCase() === 'xóa giỏ hàng')) {
            clearChatCart();
            return;
        }

        if (state.mode === 'live') {
            sendToLiveSupport(message);
        } else {
            sendToBot(message);
        }
    }

    function sendToBot(message) {
        // Show typing indicator
        showTypingIndicator();
        state.isTyping = true;

        // Disable input
        $('#chatbot-input').prop('disabled', true);
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
                $('#chatbot-input').prop('disabled', false);
                $('.chatbot-send').prop('disabled', false);
                $('#chatbot-input').focus();
            }
        });
    }

    function sendToLiveSupport(message) {
        // Disable input temporarily
        $('#chatbot-input').prop('disabled', true);
        $('.chatbot-send').prop('disabled', true);

        $.ajax({
            url: CONFIG.sendCustomerMessageEndpoint,
            method: 'POST',
            data: { message: message },
            success: function (response) {
                if (response.success) {
                    // Message sent successfully - already displayed
                    console.log('Message sent to support');
                } else {
                    addSystemMessage('❌ Không thể gửi tin nhắn. ' + (response.message || ''));
                }
            },
            error: function () {
                addSystemMessage('❌ Lỗi kết nối. Vui lòng thử lại!');
            },
            complete: function () {
                $('#chatbot-input').prop('disabled', false);
                $('.chatbot-send').prop('disabled', false);
                $('#chatbot-input').focus();
            }
        });
    }

    // ===== LIVE CHAT FUNCTIONS =====
    function loadChatHistory() {
        $.get(CONFIG.getChatHistoryEndpoint, function (response) {
            if (response.success && response.messages && response.messages.length > 0) {
                response.messages.forEach(function (msg) {
                    if (msg.IsFromCustomer) {
                        addUserMessage(msg.Message, msg.SentTime, true);
                    } else {
                        addStaffMessage(msg.SenderName, msg.Message, msg.SentTime, true);
                    }
                });
                scrollToBottom();
            }
        });
    }

    function checkUnreadMessages() {
        $.get(CONFIG.getUnreadCountEndpoint, function (response) {
            if (response.success && response.count > 0) {
                state.unreadCount = response.count;
                updateBadge(response.count);
            }
        });
    }

    function markAsRead() {
        $.post(CONFIG.markAsReadEndpoint);
    }

    function showLiveChatWelcome() {
        const html = `
            <div class="message system">
                <div class="message-content">
                    <div class="message-bubble system-bubble">
                        <i class="fas fa-headset"></i>
                        <strong>Chế độ chat với nhân viên</strong><br>
                        Tin nhắn của bạn sẽ được gửi đến đội ngũ hỗ trợ.<br>
                        Thời gian phản hồi: 1-5 phút.
                    </div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        
        addQuickReplies([
            '👋 Xin chào',
            '❓ Tôi cần hỗ trợ',
            '📋 Hỏi về đơn hàng',
            '📅 Hỏi về đặt bàn'
        ]);
    }

    // ===== MESSAGE DISPLAY =====
    function addUserMessage(text, time, isHistory) {
        time = time || getCurrentTime();
        const html = `
            <div class="message user" style="opacity: ${isHistory ? 1 : 0}; transform: translateX(${isHistory ? 0 : 20}px);">
                <div class="message-content">
                    <div class="message-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        
        if (!isHistory) {
            // Animate in
            $('.message.user').last().animate({
                opacity: 1
            }, 300).css('transform', 'translateX(0)');
        }
        
        scrollToBottom();

        // Save to history
        if (!isHistory) {
            state.messageHistory.push({ role: 'user', content: text, time: time });
        }
    }

    function addBotMessage(text, time) {
        time = time || getCurrentTime();
        const formattedText = formatBotResponse(text);
        const html = `
            <div class="message bot" style="opacity: 0; transform: translateX(-20px);">
                <div class="bot-avatar">
                    <i class="fas fa-robot"></i>
                </div>
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

    function addStaffMessage(staffName, text, time, isHistory) {
        time = time || getCurrentTime();
        const html = `
            <div class="message staff" style="opacity: ${isHistory ? 1 : 0}; transform: translateX(${isHistory ? 0 : -20}px);">
                <div class="staff-avatar">
                    <i class="fas fa-headset"></i>
                </div>
                <div class="message-content">
                    <div class="message-sender">${escapeHtml(staffName)}</div>
                    <div class="message-bubble staff-bubble">${escapeHtml(text)}</div>
                    <div class="message-time">${time}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        
        if (!isHistory) {
            // Animate in
            $('.message.staff').last().animate({
                opacity: 1
            }, 300).css('transform', 'translateX(0)');
        }
        
        scrollToBottom();
    }

    function addSystemMessage(text) {
        const html = `
            <div class="message system">
                <div class="message-content">
                    <div class="message-bubble system-bubble">${text}</div>
                </div>
            </div>
        `;
        $('.chatbot-messages').append(html);
        scrollToBottom();
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
                    <div class="suggestion-actions">
                        <button class="btn-add-cart" data-id="${id}" data-name="${escapeHtml(name)}" title="Thêm vào giỏ">
                            <i class="fas fa-cart-plus"></i>
                        </button>
                        <button class="btn-view-dish" data-id="${id}" title="Xem chi tiết">
                            <i class="fas fa-eye"></i>
                        </button>
                    </div>
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

        // Add to cart button
        $('.btn-add-cart').off('click').on('click', function (e) {
            e.stopPropagation();
            const id = $(this).data('id');
            const name = $(this).data('name');
            addToCartFromButton(id, name);
        });

        $('.suggestion-card').off('click').on('click', function (e) {
            if (!$(e.target).closest('.btn-view-dish').length && !$(e.target).closest('.btn-add-cart').length) {
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
        if ($('.typing-message').length > 0) return;
        
        const icon = state.mode === 'live' ? 'fa-headset' : 'fa-robot';
        const html = `
            <div class="message bot typing-message" style="opacity: 0;">
                <div class="bot-avatar">
                    <i class="fas ${icon}"></i>
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

        const welcomeText = `👋 ${greeting}! Mình là **LDP Bot**.\n\nMình có thể giúp bạn:\n🍽️ Tìm món ăn & gợi ý\n🛒 **Đặt món** nhanh qua chat\n📅 **Đặt bàn** trực tiếp\n🎤 Hỗ trợ giọng nói\n\nNếu cần nhân viên, chuyển sang chế độ **"Nhân viên"** ở trên.`;
        
        addBotMessage(welcomeText);
        
        addQuickReplies([
            '🔥 Top món bán chạy',
            '🛒 Đặt món ăn',
            '📅 Đặt bàn',
            '💰 Món dưới 100k',
            '👨‍💼 Chat với nhân viên'
        ]);
    }

    // ===== QUICK SUGGESTIONS =====
    function loadQuickSuggestions() {
        $.get(CONFIG.suggestionsEndpoint, function (response) {
            if (response.success) {
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

    // ===== NOTIFICATIONS =====
    function showNotification(title, message) {
        if ('Notification' in window && Notification.permission === 'granted') {
            new Notification(title, {
                body: message,
                icon: '/images/logo.png'
            });
        }
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

    // ===== CHATBOT ORDERING FUNCTIONS =====

    /**
     * Thêm món vào giỏ qua nút bấm trên suggestion card
     */
    function addToCartFromButton(menuItemId, itemName) {
        showTypingIndicator();

        $.ajax({
            url: CONFIG.addToCartEndpoint,
            method: 'POST',
            data: {
                menuItemId: menuItemId,
                quantity: 1,
                sessionId: state.sessionId
            },
            success: function (response) {
                hideTypingIndicator();

                if (response.success) {
                    if (response.sessionId) {
                        state.sessionId = response.sessionId;
                        localStorage.setItem('chatbot_session_id', response.sessionId);
                    }

                    addBotMessage(response.response);

                    if (response.quickReplies && response.quickReplies.length > 0) {
                        addQuickReplies(response.quickReplies);
                    }

                    // Update cart badge
                    if (response.metadata && response.metadata.cart_count) {
                        updateCartBadge(response.metadata.cart_count);
                    }
                } else {
                    addBotMessage(response.response || '❌ Không thể thêm món. Vui lòng thử lại!');
                }
            },
            error: function () {
                hideTypingIndicator();
                addBotMessage('❌ Lỗi kết nối. Vui lòng thử lại!');
            }
        });
    }

    /**
     * Xóa giỏ hàng chatbot
     */
    function clearChatCart() {
        $.ajax({
            url: CONFIG.clearCartEndpoint,
            method: 'POST',
            data: { sessionId: state.sessionId },
            success: function (response) {
                if (response.success) {
                    addBotMessage(response.response);
                    updateCartBadge(0);
                    if (response.quickReplies && response.quickReplies.length > 0) {
                        addQuickReplies(response.quickReplies);
                    }
                }
            }
        });
    }

    /**
     * Cập nhật badge giỏ hàng trên chatbot header
     */
    function updateCartBadge(count) {
        let badge = $('.chatbot-cart-badge');
        if (badge.length === 0 && count > 0) {
            $('.chatbot-header').append('<span class="chatbot-cart-badge">🛒 ' + count + '</span>');
        } else if (count > 0) {
            badge.text('🛒 ' + count).show();
        } else {
            badge.hide();
        }
    }

    // ===== EXPOSE PUBLIC API =====
    window.LDPChatbot = {
        open: openChatbot,
        close: closeChatbot,
        toggle: toggleChatbot,
        switchToLive: function() { switchMode('live'); },
        switchToBot: function() { switchMode('bot'); },
        addToCart: addToCartFromButton,
        clearCart: clearChatCart,
        getState: function() { return state; }
    };

})(jQuery);
