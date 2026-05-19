(() => {
    const config = window.SentroIdleConfig;
    if (!config || !config.enabled) return;

    const warningMs = Number(config.warningMs || 45000);
    const timeoutMs = Number(config.timeoutMs || 60000);
    const maxReauthAttempts = Number(config.maxReauthAttempts || 3);

    const modal = document.getElementById("idleReauthModal");
    const countdownEl = document.getElementById("idleCountdown");
    const passwordInput = document.getElementById("idlePassword");
    const errorEl = document.getElementById("idleError");
    const submitBtn = document.getElementById("idleReauthSubmit");
    const logoutBtn = document.getElementById("idleForceLogout");
    const cancelBtn = document.getElementById("idleCancel");

    if (!modal || !countdownEl || !passwordInput || !submitBtn || !logoutBtn) return;

    const activityEvents = ["mousemove", "mousedown", "keydown", "scroll", "touchstart", "pointerdown"];
    const logoutForm = document.getElementById(config.logoutFormId || "");
    let lastActivity = Date.now();
    let warningShown = false;
    let reauthAttempts = 0;
    let tickHandle = null;
    let submitting = false;

    const closeModal = () => {
        modal.classList.add("hidden");
        modal.classList.remove("flex");
        passwordInput.value = "";
        errorEl.classList.add("hidden");
        errorEl.textContent = "";
    };

    const openModal = () => {
        modal.classList.remove("hidden");
        modal.classList.add("flex");
        passwordInput.value = "";
        errorEl.classList.add("hidden");
        errorEl.textContent = "";
        window.setTimeout(() => passwordInput.focus(), 20);
    };

    const logoutNow = () => {
        if (logoutForm) {
            logoutForm.submit();
            return;
        }
        window.location.href = "/Identity/Account/Logout";
    };

    const setError = (message) => {
        errorEl.textContent = message;
        errorEl.classList.remove("hidden");
    };

    const markActivity = () => {
        if (warningShown) return;
        lastActivity = Date.now();
    };

    const secondsRemaining = () => Math.max(0, Math.ceil((timeoutMs - (Date.now() - lastActivity)) / 1000));

    const checkIdle = () => {
        const idleFor = Date.now() - lastActivity;

        if (!warningShown && idleFor >= warningMs) {
            warningShown = true;
            openModal();
        }

        if (warningShown) {
            const secs = secondsRemaining();
            countdownEl.textContent = String(secs);
            if (secs <= 0) {
                logoutNow();
            }
        }
    };

    const resumeSession = async () => {
        if (submitting) return;
        const password = passwordInput.value;
        if (!password) {
            setError("Please enter your password.");
            return;
        }

        const tokenInput = logoutForm ? logoutForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        const token = tokenInput ? tokenInput.value : "";
        if (!token) {
            setError("Security token is missing. Please log in again.");
            return;
        }

        submitting = true;
        submitBtn.disabled = true;
        submitBtn.textContent = "Checking...";

        try {
            const body = new URLSearchParams();
            body.append("password", password);
            body.append("__RequestVerificationToken", token);

            const res = await fetch(config.reauthUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
                body: body.toString()
            });

            if (res.ok) {
                warningShown = false;
                reauthAttempts = 0;
                lastActivity = Date.now();
                closeModal();
                return;
            }

            reauthAttempts += 1;
            if (res.status === 423 || reauthAttempts >= maxReauthAttempts) {
                logoutNow();
                return;
            }

            setError("Incorrect password. Try again.");
        } catch (_) {
            setError("Could not verify session. Check your connection.");
        } finally {
            submitting = false;
            submitBtn.disabled = false;
            submitBtn.textContent = "Re-enter Password";
        }
    };

    activityEvents.forEach((eventName) => {
        document.addEventListener(eventName, markActivity, { passive: true });
    });

    submitBtn.addEventListener("click", resumeSession);
    passwordInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            resumeSession();
        }
    });

    logoutBtn.addEventListener("click", logoutNow);
    if (cancelBtn) {
        cancelBtn.addEventListener("click", () => {
            logoutNow();
        });
    }

    tickHandle = window.setInterval(checkIdle, 1000);
    window.addEventListener("beforeunload", () => {
        if (tickHandle) window.clearInterval(tickHandle);
    });
})();
