async function loginUser(username, password) {
    const res = await post("/login", { username, password });
    const data = await res.json();

    document.getElementById("login-message").textContent = data.message;

    if (data.require2FA) {
        // Show 2FA section
        document.getElementById("two-factor-section").style.display = "flex";
        document.getElementById("login-form").style.display = "none";

        // Draw QR code on canvas
        if (data.qr_data) {
            const matrix = encodeQR(data.qr_data);
            drawQR(matrix, "qrCanvas");
        }

        // Handle OTP verification
        const verifyBtn = document.getElementById("verify-otp");
        verifyBtn.onclick = async () => {
            const otpInput = document.getElementById("otp").value;
            const otpRes = await post("/login", { username, password, totp_code: otpInput });
            const otpData = await otpRes.json();
            document.getElementById("login-message").textContent = otpData.message;
            if (otpData.success) {
                setTimeout(() => window.location.href = "dashboard.html", 1500);
            }
        };
    } else if (data.success) {
        setTimeout(() => window.location.href = "dashboard.html", 1500);
    }
}

document.getElementById("login-form").addEventListener("submit", e => {
    e.preventDefault();
    const username = document.getElementById("username").value;
    const password = document.getElementById("password").value;
    loginUser(username, password);
});
