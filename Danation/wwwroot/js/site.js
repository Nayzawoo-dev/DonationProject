/**
 * Danation — Global JavaScript
 * AJAX helpers, Notiflix setup, anti-forgery, notification polling
 */

// ============================
//  Notiflix Global Config
// ============================
Notiflix.Notify.init({
    width: '320px',
    position: 'right-top',
    distance: '12px',
    opacity: 1,
    borderRadius: '10px',
    rtl: false,
    timeout: 4000,
    messageMaxLength: 200,
    backOverlay: false,
    plainText: true,
    showOnlyTheLastOne: false,
    clickToClose: true,
    pauseOnHover: true,
    fontFamily: 'Inter, sans-serif',
    fontSize: '14px',
    success: { background: '#43a047', textColor: '#fff', notiflixIconColor: '#fff' },
    failure: { background: '#e53935', textColor: '#fff', notiflixIconColor: '#fff' },
    warning: { background: '#fb8c00', textColor: '#fff', notiflixIconColor: '#fff' },
    info:    { background: '#5c6bc0', textColor: '#fff', notiflixIconColor: '#fff' }
});

Notiflix.Confirm.init({
    borderRadius: '10px',
    fontFamily: 'Inter, sans-serif',
    titleColor: '#1a1a2e',
    okButtonBackground: '#e53935',
    cancelButtonBackground: '#718096'
});

Notiflix.Loading.init({
    svgColor: '#5c6bc0',
    fontFamily: 'Inter, sans-serif',
    backgroundColor: 'rgba(0,0,0,0.5)'
});

// ============================
//  jQuery AJAX Global Setup
// ============================
$(function () {
    // Set anti-forgery token on all AJAX requests
    var token = $('input[name="__RequestVerificationToken"]').val();
    if (token) {
        $.ajaxSetup({
            headers: { 'RequestVerificationToken': token }
        });
    }

    // Global AJAX error handler
    $(document).ajaxError(function (event, jqXHR) {
        Notiflix.Loading.remove();
        if (jqXHR.status === 401) {
            Notiflix.Notify.warning('Please log in to continue.');
            setTimeout(function () { window.location.href = '/Account/Login'; }, 1500);
        } else if (jqXHR.status === 403) {
            Notiflix.Notify.failure('You do not have permission to perform this action.');
        } else if (jqXHR.status === 429) {
            Notiflix.Notify.warning('Too many requests. Please wait a moment.');
        } else if (jqXHR.status >= 500) {
            Notiflix.Notify.failure('A server error occurred. Please try again.');
        }
    });
});

// ============================
//  AJAX Helper Functions
// ============================
var Danation = window.Danation || {};

/**
 * Standard AJAX POST helper
 * @param {string} url
 * @param {object} data
 * @param {function} onSuccess - called with (response)
 * @param {function} [onError]
 */
Danation.post = function (url, data, onSuccess, onError) {
    var token = $('input[name="__RequestVerificationToken"]').val();
    Notiflix.Loading.pulse('Please wait...');
    $.ajax({
        url: url,
        type: 'POST',
        data: data,
        headers: { 'RequestVerificationToken': token }
    }).done(function (res) {
        Notiflix.Loading.remove();
        if (res && res.success) {
            if (onSuccess) onSuccess(res);
        } else {
            var msg = (res && res.message) ? res.message : 'An error occurred.';
            Notiflix.Notify.failure(msg);
            if (onError) onError(res);
        }
    }).fail(function (jqXHR) {
        Notiflix.Loading.remove();
        var msg = 'Request failed. Please try again.';
        try {
            var r = JSON.parse(jqXHR.responseText);
            if (r && r.message) msg = r.message;
        } catch (e) { }
        Notiflix.Notify.failure(msg);
        if (onError) onError(null);
    });
};

/**
 * Standard AJAX POST with FormData (for file uploads)
 * @param {string} url
 * @param {FormData} formData
 * @param {function} onSuccess
 * @param {function} [onError]
 */
Danation.upload = function (url, formData, onSuccess, onError) {
    var token = $('input[name="__RequestVerificationToken"]').val();
    formData.append('__RequestVerificationToken', token);
    Notiflix.Loading.pulse('Uploading...');
    $.ajax({
        url: url,
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        headers: { 'RequestVerificationToken': token }
    }).done(function (res) {
        Notiflix.Loading.remove();
        if (res && res.success) {
            if (onSuccess) onSuccess(res);
        } else {
            var msg = (res && res.message) ? res.message : 'Upload failed.';
            Notiflix.Notify.failure(msg);
            if (onError) onError(res);
        }
    }).fail(function (jqXHR) {
        Notiflix.Loading.remove();
        var msg = 'Upload failed. Please try again.';
        try {
            var r = JSON.parse(jqXHR.responseText);
            if (r && r.message) msg = r.message;
        } catch (e) { }
        Notiflix.Notify.failure(msg);
        if (onError) onError(null);
    });
};

/**
 * Confirm + AJAX POST helper (for destructive actions)
 * @param {string} confirmMessage
 * @param {string} url
 * @param {object} data
 * @param {function} onSuccess
 */
Danation.confirmPost = function (confirmMessage, url, data, onSuccess) {
    Notiflix.Confirm.show(
        'Confirm Action',
        confirmMessage,
        'Confirm',
        'Cancel',
        function () {
            Danation.post(url, data, onSuccess);
        }
    );
};

/**
 * Escape HTML to prevent XSS in dynamic content
 */
Danation.escHtml = function (str) {
    return $('<span>').text(str || '').html();
};

/**
 * Format currency (Myanmar Kyat)
 */
Danation.formatMMK = function (amount) {
    if (amount === null || amount === undefined) return '—';
    return parseFloat(amount).toLocaleString('en-US') + ' MMK';
};

/**
 * Status badge HTML
 */
Danation.statusBadge = function (status) {
    var map = {
        'PENDING':    'badge-pending',
        'OPEN':       'badge-open',
        'GOAL_REACHED': 'badge-goal',
        'CLOSED':     'badge-closed',
        'COMPLETED':  'badge-completed',
        'REJECTED':   'badge-rejected',
        'APPROVED':   'badge-approved'
    };
    var cls = map[status] || 'bg-secondary text-white';
    return '<span class="badge ' + cls + '">' + Danation.escHtml(status) + '</span>';
};

// Expose globally
window.Danation = Danation;
