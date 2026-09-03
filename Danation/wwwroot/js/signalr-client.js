/**
 * Danation — SignalR Client Management
 * Real-time connection, event dispatching, reconnection & graceful fallbacks
 */

(function () {
    'use strict';

    if (typeof signalR === 'undefined') {
        console.warn('SignalR library not loaded. Real-time updates disabled; AJAX remains fully active.');
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/app')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    // ============================
    //  Connection Lifecycle
    // ============================
    connection.onreconnecting(function (error) {
        console.warn('SignalR: Reconnecting to server...', error);
    });

    connection.onreconnected(function (connectionId) {
        console.log('SignalR: Reconnected successfully. ConnectionId: ' + connectionId);
        // Re-join campaign group if viewing a campaign
        if (window.__currentCampaignId) {
            connection.invoke('JoinCampaign', window.__currentCampaignId).catch(function (e) {
                console.error('Failed to re-join campaign group:', e);
            });
        }
    });

    connection.onclose(function (error) {
        console.warn('SignalR: Connection closed. Retrying...', error);
        setTimeout(startSignalR, 5000);
    });

    function startSignalR() {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            connection.start().then(function () {
                console.log('SignalR: Connected.');
                if (window.__currentCampaignId) {
                    connection.invoke('JoinCampaign', window.__currentCampaignId).catch(function (e) {
                        console.error('Failed to join campaign group:', e);
                    });
                }
            }).catch(function (err) {
                console.warn('SignalR connection failed, will retry in 5s:', err);
                setTimeout(startSignalR, 5000);
            });
        }
    }

    // ============================
    //  Global Event Handlers
    // ============================

    // 1. User Notification
    connection.on('ReceiveNotification', function (data) {
        // Update bell badge
        if (data.unreadCount > 0) {
            $('#notifBadge').text(data.unreadCount > 99 ? '99+' : data.unreadCount).removeClass('d-none');
        } else {
            $('#notifBadge').addClass('d-none');
        }

        // Show friendly toast
        Notiflix.Notify.info(data.title + ': ' + data.message);

        // Prepend to dropdown list if present
        $('#notifEmpty').hide();
        var itemHtml = '<div class="notif-item px-3 py-2 border-bottom bg-light-blue" data-id="' + data.id + '">' +
            '<div class="d-flex justify-content-between align-items-start">' +
            '<strong class="small">' + Danation.escHtml(data.title) + '</strong>' +
            '<span class="text-muted" style="font-size:11px;white-space:nowrap;margin-left:8px;">' + Danation.escHtml(data.relativeTime || 'Just now') + '</span>' +
            '</div>' +
            '<p class="mb-0 small text-muted mt-1">' + Danation.escHtml(data.message) + '</p>' +
            '</div>';
        $('#notifList').find('.notif-item[data-id="' + data.id + '"]').remove();
        $('#notifList').prepend(itemHtml);

        // Trigger page-level hook if defined
        $(document).trigger('receiveNotification', [data]);
    });

    // 2. Notification Read Synced
    connection.on('NotificationReadUpdated', function (data) {
        if (data.unreadCount > 0) {
            $('#notifBadge').text(data.unreadCount > 99 ? '99+' : data.unreadCount).removeClass('d-none');
        } else {
            $('#notifBadge').addClass('d-none');
        }

        if (data.allRead) {
            $('#notifList .notif-item').removeClass('bg-light-blue');
        } else if (data.notificationId) {
            $('#notifList .notif-item[data-id="' + data.notificationId + '"]').removeClass('bg-light-blue');
        }

        $(document).trigger('notificationReadUpdated', [data]);
    });

    // 3. Campaign Status Changed
    connection.on('CampaignStatusChanged', function (data) {
        $(document).trigger('campaignStatusChanged', [data]);
    });

    // 4. Campaign Donation / Progress Updated
    connection.on('CampaignDonationUpdated', function (data) {
        $(document).trigger('campaignDonationUpdated', [data]);
    });

    // 5. Donation Status Changed (Donor / Admin)
    connection.on('DonationStatusChanged', function (data) {
        $(document).trigger('donationStatusChanged', [data]);
    });

    // 6. Admin: New Donation Created
    connection.on('DonationCreated', function (data) {
        Notiflix.Notify.warning('New donation submitted by ' + (data.donorName || 'a donor') + ' for "' + (data.campaignTitle || 'Campaign') + '"');
        $(document).trigger('donationCreated', [data]);
    });

    // 7. Admin: New Campaign Created
    connection.on('CampaignCreated', function (data) {
        Notiflix.Notify.info('New campaign submitted: "' + (data.title || 'Campaign') + '" by ' + (data.ownerName || 'User'));
        $(document).trigger('campaignCreated', [data]);
    });

    // 8. Admin: Dashboard Stats Updated
    connection.on('AdminDashboardStats', function (data) {
        $(document).trigger('adminDashboardStats', [data]);
    });

    // Expose global controller
    window.DanationHub = {
        connection: connection,
        joinCampaign: function (campaignId) {
            window.__currentCampaignId = campaignId;
            if (connection.state === signalR.HubConnectionState.Connected) {
                connection.invoke('JoinCampaign', campaignId).catch(function (e) {
                    console.error('Failed to join campaign group:', e);
                });
            }
        },
        leaveCampaign: function (campaignId) {
            window.__currentCampaignId = null;
            if (connection.state === signalR.HubConnectionState.Connected) {
                connection.invoke('LeaveCampaign', campaignId).catch(function (e) {
                    console.error('Failed to leave campaign group:', e);
                });
            }
        }
    };

    // Auto-start connection on page load
    $(function () {
        startSignalR();
    });

})();
