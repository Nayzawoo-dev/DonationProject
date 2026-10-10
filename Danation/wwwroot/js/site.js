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
    fontFamily: "'Noto Sans Myanmar', 'Inter', sans-serif",
    fontSize: '14px',
    success: { background: '#2e7d32', textColor: '#fff', notiflixIconColor: '#fff' },
    failure: { background: '#d32f2f', textColor: '#fff', notiflixIconColor: '#fff' },
    warning: { background: '#ed6c02', textColor: '#fff', notiflixIconColor: '#fff' },
    info:    { background: '#0288d1', textColor: '#fff', notiflixIconColor: '#fff' }
});

Notiflix.Confirm.init({
    borderRadius: '10px',
    fontFamily: "'Noto Sans Myanmar', 'Inter', sans-serif",
    titleColor: '#1b3b22',
    okButtonBackground: '#2e7d32',
    cancelButtonBackground: '#718096'
});

Notiflix.Loading.init({
    svgColor: '#2e7d32',
    fontFamily: "'Noto Sans Myanmar', 'Inter', sans-serif",
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
            Notiflix.Notify.warning('ကျေးဇူးပြု၍ အကောင့်ဝင်ရောက်ပေးပါ။');
            setTimeout(function () { window.location.href = '/Account/Login'; }, 1500);
        } else if (jqXHR.status === 403) {
            Notiflix.Notify.failure('ဤလုပ်ဆောင်ချက်ကို ဆောင်ရွက်ရန် လုပ်ပိုင်ခွင့်မရှိပါ။');
        } else if (jqXHR.status === 429) {
            Notiflix.Notify.warning('တောင်းဆိုမှုများ ပြားလွန်းနေပါသည်။ ခေတ္တစောင့်ဆိုင်းပေးပါ။');
        } else if (jqXHR.status >= 500) {
            Notiflix.Notify.failure('ဆာဗာတွင် ချို့ယွင်းချက် ဖြစ်ပေါ်နေပါသည်။ နောက်တစ်ကြိမ် ထပ်မံကြိုးစားကြည့်ပါ။');
        }
    });
});

// ============================
//  AJAX Helper Functions
// ============================
var Danation = window.Danation || {};

/**
 * Get the current Anti-Forgery Token
 */
Danation.getCsrfToken = function () {
    return $('input[name="__RequestVerificationToken"]').first().val() || '';
};

/**
 * Standard AJAX POST helper
 * @param {string} url
 * @param {object|string} data
 * @param {function} onSuccess - called with (response)
 * @param {function} [onError]
 */
Danation.post = function (url, data, onSuccess, onError) {
    var token = Danation.getCsrfToken();
    Notiflix.Loading.pulse('ခေတ္တစောင့်ဆိုင်းပါ...');
    $.ajax({
        url: url,
        type: 'POST',
        data: data,
        headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }
    }).done(function (res) {
        Notiflix.Loading.remove();
        if (res && res.success) {
            if (onSuccess) onSuccess(res);
        } else {
            var msg = (res && res.message) ? res.message : 'မှားယွင်းမှုတစ်ခု ဖြစ်ပေါ်ခဲ့ပါသည်။';
            Notiflix.Notify.failure(msg);
            if (onError) onError(res);
        }
    }).fail(function (jqXHR) {
        Notiflix.Loading.remove();
        var msg = 'တောင်းဆိုမှု မအောင်မြင်ပါ။ နောက်တစ်ကြိမ် ထပ်မံကြိုးစားကြည့်ပါ။';
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
    var token = Danation.getCsrfToken();
    if (token && !formData.has('__RequestVerificationToken')) {
        formData.append('__RequestVerificationToken', token);
    }
    Notiflix.Loading.pulse('ဖိုင်တင်နေပါသည်...');
    $.ajax({
        url: url,
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }
    }).done(function (res) {
        Notiflix.Loading.remove();
        if (res && res.success) {
            if (onSuccess) onSuccess(res);
        } else {
            var msg = (res && res.message) ? res.message : 'ဖိုင်တင်ခြင်း မအောင်မြင်ပါ။';
            Notiflix.Notify.failure(msg);
            if (onError) onError(res);
        }
    }).fail(function (jqXHR) {
        Notiflix.Loading.remove();
        var msg = 'ဖိုင်တင်ခြင်း မအောင်မြင်ပါ။ နောက်တစ်ကြိမ် ထပ်မံကြိုးစားကြည့်ပါ။';
        try {
            var r = JSON.parse(jqXHR.responseText);
            if (r && r.message) msg = r.message;
        } catch (e) { }
        Notiflix.Notify.failure(msg);
        if (onError) onError(null);
    });
};

/**
 * Standard AJAX Partial View loader
 * @param {string} url
 * @param {string|jQuery} container
 * @param {function} [onSuccess]
 * @param {function} [onError]
 */
Danation.loadPartial = function (url, container, onSuccess, onError) {
    var $el = $(container);
    $el.css('opacity', '0.5');
    $.ajax({
        url: url,
        type: 'GET',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).done(function (html) {
        $el.html(html).css('opacity', '1');
        if (onSuccess) onSuccess(html);
    }).fail(function (jqXHR) {
        $el.css('opacity', '1');
        Notiflix.Notify.failure('အချက်အလက်များ ဆွဲယူခြင်း မအောင်မြင်ပါ။ စာမျက်နှာကို ပြန်လည်ဖွင့်ကြည့်ပါ။');
        if (onError) onError(jqXHR);
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
        'အတည်ပြုချက် တောင်းခံခြင်း',
        confirmMessage,
        'အတည်ပြုမည်',
        'မလုပ်တော့ပါ',
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
    return parseFloat(amount).toLocaleString('en-US') + ' ကျပ်';
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
    var textMap = {
        'PENDING':      'စိစစ်ဆဲ',
        'OPEN':         'ဖွင့်လှစ်ဆဲ',
        'GOAL_REACHED': 'ပန်းတိုင်ပြည့်',
        'CLOSED':       'ပိတ်သိမ်းပြီး',
        'COMPLETED':    'ပြီးမြောက်ပြီး',
        'REJECTED':     'ငြင်းပယ်ပြီး',
        'APPROVED':     'အတည်ပြုပြီး'
    };
    var cls = map[status] || 'bg-secondary text-white';
    var label = textMap[status] || status;
    return '<span class="badge ' + cls + '">' + Danation.escHtml(label) + '</span>';
};

// Expose globally
window.Danation = Danation;
