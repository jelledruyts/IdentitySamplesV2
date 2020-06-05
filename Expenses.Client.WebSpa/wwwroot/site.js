"use strict";

var ExpensesClientWebSpa = (function ($) {
    var msalConfig = {
        auth: {
            clientId: appsettings.aadClientId,
            authority: appsettings.aadAuthority
        },
        system: {
            loggerOptions: {
                loggerCallback: function (level, message, containsPii) {
                    if (containsPii) {
                        return;
                    }
                    switch (level) {
                        case 0 /*LogLevel.Error*/:
                            console.error(message);
                            return;
                        case 2 /*LogLevel.Info*/:
                            console.info(message);
                            return;
                        case 3 /*LogLevel.Verbose*/:
                            console.debug(message);
                            return;
                        case 1 /*LogLevel.Warning*/:
                            console.warn(message);
                            return;
                    }
                },
                piiLoggingEnabled: false
            }
        },
        cache: {
            cacheLocation: "localStorage",
            storeAuthStateInCookie: true
        }
    };
    var clientApplication = new msal.PublicClientApplication(msalConfig);

    var performSignIn = function () {
        clientApplication.loginPopup({ scopes: appsettings.scopes })
            .then(function (loginResponse) {
                updateUI();
            }).catch(function (error) {
                alert("Could not sign in: " + error);
            });
    }

    var ensureSignedIn = function () {
        if (!clientApplication.getAccount()) {
            performSignIn();
        }
    };

    var updateUI = function () {
        var account = clientApplication.getAccount();
        if (account) {
            $("#signInLink").hide();
            $("#userNameText").text("Hello " + account.name + "!");
            $("#userNameText").show();
            $("#signOutLink").show();
            $("#identityInfoPanel").show();
        } else {
            $("#signInLink").show();
            $("#userNameText").hide();
            $("#signOutLink").hide();
            $("#identityInfoPanel").hide();
        }
    };

    var getIdentityInfo = function () {
        $("#identityInfoText").text("Loading...");
        $("#identityInfoText").show();

        var getIdentityInfoFromWebApi = function (accessToken) {
            $.support.cors = true;
            $.ajax({
                type: "GET",
                url: appsettings.expensesApiBaseUrl + "api/account/identity",
                crossDomain: true,
                headers: {
                    "Authorization": "Bearer " + accessToken,
                },
            }).done(function (data) {
                $("#identityInfoText").text(JSON.stringify(data, null, 2));
                $("#identityInfoText").show();
            }).fail(function (jqXHR, textStatus) {
                $("#identityInfoText").text("Could not get identity info: " + textStatus);
            })
        };

        ensureSignedIn();

        clientApplication.acquireTokenSilent({ scopes: appsettings.scopes }).then(function (accessTokenResponse) {
            getIdentityInfoFromWebApi(accessTokenResponse.accessToken);
        }).catch(function (error) {
            if (error.name === "InteractionRequiredAuthError") {
                clientApplication.acquireTokenPopup({ scopes: appsettings.scopes }).then(function (accessTokenResponse) {
                    getIdentityInfoFromWebApi(accessTokenResponse.accessToken);
                }).catch(function (error) {
                    alert("Could not acquire token: " + error);
                });
            }
            alert("Could not acquire token: " + error);
        });
    };

    var init = function () {
        $("#signInLink").on("click", function (event) {
            event.preventDefault();
            ensureSignedIn();
        });
        $("#signOutLink").on("click", function (event) {
            event.preventDefault();
            clientApplication.logout();
        });
        $("#getIdentityInfoButton").on("click", function (event) {
            event.preventDefault();
            getIdentityInfo();
        });
        updateUI();
    };

    return {
        init: init
    };
})(jQuery);

$(document).ready(function () {
    ExpensesClientWebSpa.init();
});