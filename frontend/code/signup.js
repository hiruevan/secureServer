async function signupUser(username, first_name, last_name, password) {
    try {
        // Frontend validation (stops empty strings)
        if (!username || username.trim().length < 3) {
            document.getElementById("signup-message").textContent =
                "Username must be at least 3 characters.";
            return;
        }

        if (!password || password.length < 12) {
            document.getElementById("signup-message").textContent =
                "Password must be at least 12 characters.";
            return;
        }

        // Email validation
        // if (!email || !email.includes("@") || !email.includes(".")) {
        //     document.getElementById("signup-message").textContent =
        //         "Please enter a valid email address.";
        //     return;
        // }

        const res = await post("/signup", {
            username,
            password,
            first_name: first_name || null,
            last_name: last_name || null,
            // email: email || null
        });

        const data = await res.json();

        document.getElementById("signup-message").textContent =
            data.message || data.detail || "Signup complete.";

        if (data.success) {
            console.log("Signup successful!");
            setTimeout(() => window.location.href = "login.html", 500);
        }

    } catch (err) {
        console.error("Signup error:", err);
        document.getElementById("signup-message").textContent =
            "An error occurred while signing up.";
    }
}

document.getElementById("signup-form").addEventListener("submit", (e) => {
    e.preventDefault();

    // Delay fixes Chrome autofill not populating password until *after* submit
    setTimeout(() => {
        const username = document.getElementById("username").value;
        const first_name = document.getElementById("first_name").value;
        const last_name = document.getElementById("last_name").value;
        // const email = document.getElementById("email").value;
        const password = document.getElementById("password").value;

        signupUser(username, first_name, last_name, password);
    }, 50);
});