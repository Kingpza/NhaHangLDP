/**
 * SweetAlert2 Helper Functions
 * Global helpers for consistent alert/notification styling across the application
 */

// Success notification
function showSuccess(title, text, callback) {
    Swal.fire({
        icon: 'success',
        title: title || 'Thành công!',
        text: text,
        confirmButtonText: 'Đóng',
        timer: 3000,
        timerProgressBar: true,
        showClass: {
            popup: 'animate__animated animate__fadeInDown'
        },
        hideClass: {
            popup: 'animate__animated animate__fadeOutUp'
        }
    }).then((result) => {
        if (callback) callback(result);
    });
}

// Error notification
function showError(title, text, callback) {
    Swal.fire({
        icon: 'error',
        title: title || 'Có lỗi xảy ra!',
        text: text,
        confirmButtonText: 'Đóng',
        showClass: {
            popup: 'animate__animated animate__shakeX'
        }
    }).then((result) => {
        if (callback) callback(result);
    });
}

// Warning notification
function showWarning(title, text, callback) {
    Swal.fire({
        icon: 'warning',
        title: title || 'Cảnh báo!',
        text: text,
        confirmButtonText: 'Đóng'
    }).then((result) => {
        if (callback) callback(result);
    });
}

// Info notification
function showInfo(title, text, callback) {
    Swal.fire({
        icon: 'info',
        title: title || 'Thông tin',
        text: text,
        confirmButtonText: 'Đóng'
    }).then((result) => {
        if (callback) callback(result);
    });
}

// Question/Confirmation dialog
function showQuestion(title, text, confirmCallback, cancelCallback) {
    Swal.fire({
        title: title || 'Câu hỏi',
        text: text,
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Có',
        cancelButtonText: 'Không',
        reverseButtons: true
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback();
        } else if (result.isDismissed && cancelCallback) {
            cancelCallback();
        }
    });
}

// Confirmation dialog
function showConfirm(title, text, confirmCallback, cancelCallback) {
    Swal.fire({
        title: title || 'Xác nhận',
        text: text,
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        reverseButtons: true
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback();
        } else if (result.isDismissed && cancelCallback) {
            cancelCallback();
        }
    });
}

// Delete confirmation
function showDeleteConfirm(title, text, confirmCallback, cancelCallback) {
    Swal.fire({
        title: title || 'Xác nhận xóa',
        text: text || 'Bạn có chắc chắn muốn xóa? Hành động này không thể hoàn tác!',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Xóa',
        confirmButtonColor: '#ef4444',
        cancelButtonText: 'Hủy',
        reverseButtons: true,
        showClass: {
            popup: 'animate__animated animate__headShake'
        }
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback();
        } else if (result.isDismissed && cancelCallback) {
            cancelCallback();
        }
    });
}

// Loading state
function showLoading(title, text) {
    Swal.fire({
        title: title || 'Đang xử lý...',
        text: text || 'Vui lòng chờ trong giây lát',
        allowOutsideClick: false,
        allowEscapeKey: false,
        allowEnterKey: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });
}

// Update loading text
function updateLoadingText(title, text) {
    Swal.update({
        title: title,
        text: text
    });
}

// Close loading
function closeLoading() {
    Swal.close();
}

// Toast notification (top-right corner)
function showToast(icon, title, position) {
    const Toast = Swal.mixin({
        toast: true,
        position: position || 'top-end',
        showConfirmButton: false,
        timer: 3000,
        timerProgressBar: true,
        didOpen: (toast) => {
            toast.addEventListener('mouseenter', Swal.stopTimer)
            toast.addEventListener('mouseleave', Swal.resumeTimer)
        }
    });

    Toast.fire({
        icon: icon || 'success',
        title: title
    });
}

// Success toast
function toastSuccess(message) {
    showToast('success', message);
}

// Error toast
function toastError(message) {
    showToast('error', message);
}

// Warning toast
function toastWarning(message) {
    showToast('warning', message);
}

// Info toast
function toastInfo(message) {
    showToast('info', message);
}

// Custom HTML dialog
function showCustomDialog(options) {
    const defaultOptions = {
        title: 'Thông báo',
        html: '',
        icon: 'info',
        confirmButtonText: 'Đóng',
        showCancelButton: false,
        cancelButtonText: 'Hủy'
    };

    Swal.fire({...defaultOptions, ...options});
}

// Input dialog
function showInputDialog(title, inputPlaceholder, confirmCallback) {
    Swal.fire({
        title: title || 'Nhập thông tin',
        input: 'text',
        inputPlaceholder: inputPlaceholder || 'Nhập...',
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        reverseButtons: true,
        inputValidator: (value) => {
            if (!value) {
                return 'Vui lòng nhập thông tin!'
            }
        }
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback(result.value);
        }
    });
}

// Textarea dialog
function showTextareaDialog(title, placeholder, confirmCallback) {
    Swal.fire({
        title: title || 'Nhập thông tin',
        input: 'textarea',
        inputPlaceholder: placeholder || 'Nhập nội dung...',
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        reverseButtons: true,
        inputValidator: (value) => {
            if (!value) {
                return 'Vui lòng nhập nội dung!'
            }
        }
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback(result.value);
        }
    });
}

// Select dialog
function showSelectDialog(title, options, confirmCallback) {
    Swal.fire({
        title: title || 'Chọn tùy chọn',
        input: 'select',
        inputOptions: options,
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        reverseButtons: true,
        inputValidator: (value) => {
            if (!value) {
                return 'Vui lòng chọn một tùy chọn!'
            }
        }
    }).then((result) => {
        if (result.isConfirmed && confirmCallback) {
            confirmCallback(result.value);
        }
    });
}

// Progress dialog
function showProgress(title, html) {
    let timerInterval;
    Swal.fire({
        title: title || 'Đang xử lý',
        html: html || 'Tiến trình: <b></b>%',
        timer: 100000,
        timerProgressBar: true,
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading();
            const b = Swal.getHtmlContainer().querySelector('b');
            timerInterval = setInterval(() => {
                const progress = Math.floor((Swal.getTimerLeft() / 100000) * 100);
                b.textContent = 100 - progress;
            }, 100);
        },
        willClose: () => {
            clearInterval(timerInterval);
        }
    });
}

// Auto close notification
function showAutoClose(title, text, timer) {
    let timerInterval;
    Swal.fire({
        title: title || 'Tự động đóng',
        html: text + '<br/><br/>Sẽ tự động đóng sau <b></b> giây.',
        timer: (timer || 5) * 1000,
        timerProgressBar: true,
        didOpen: () => {
            const b = Swal.getHtmlContainer().querySelector('b');
            timerInterval = setInterval(() => {
                b.textContent = Math.ceil(Swal.getTimerLeft() / 1000);
            }, 100);
        },
        willClose: () => {
            clearInterval(timerInterval);
        }
    });
}

// Step-by-step wizard
function showWizard(steps, onComplete) {
    const Queue = Swal.mixin({
        confirmButtonText: 'Tiếp theo &rarr;',
        cancelButtonText: 'Quay lại',
        progressSteps: Array.from({length: steps.length}, (_, i) => i + 1),
        input: 'text',
        inputAttributes: {
            required: true
        },
        reverseButtons: true,
        validationMessage: 'Trường này là bắt buộc'
    });

    (async () => {
        const values = [];
        let currentStep = 0;

        for (const step of steps) {
            const result = await Queue.fire({
                ...step,
                currentProgressStep: currentStep
            });

            if (result.isConfirmed) {
                values.push(result.value);
                currentStep++;
            } else {
                return;
            }
        }

        if (onComplete) {
            onComplete(values);
        }
    })();
}

// Override native alert
window.alert = function(message) {
    Swal.fire({
        title: 'Thông báo',
        text: message,
        icon: 'info',
        confirmButtonText: 'Đóng'
    });
};

// Override native confirm - returns Promise
window.confirm = function(message) {
    return Swal.fire({
        title: 'Xác nhận',
        text: message,
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        reverseButtons: true
    }).then((result) => result.isConfirmed);
};

// Override native prompt
window.prompt = function(message, defaultValue) {
    return Swal.fire({
        title: 'Nhập thông tin',
        input: 'text',
        inputValue: defaultValue || '',
        inputPlaceholder: message,
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        reverseButtons: true
    }).then((result) => {
        if (result.isConfirmed) {
            return result.value;
        }
        return null;
    });
};
