// --- DOM Elements ---
const oldPasswordEl = document.getElementById("old_password");
const newPasswordEl = document.getElementById("new_password");
const confirmPasswordEl = document.getElementById("confirm_password");
const changeBtn = document.getElementById("submitButton");
const messageBox = document.getElementById("messageBox");
const form = document.getElementById('passwordChangeForm');

/**
 * Utility function to display messages to the user using the styled box.
 * @param {string} message The text to display.
 * @param {boolean} isSuccess Whether the message indicates success.
 */
function displayMessage(message, isSuccess) {
    messageBox.textContent = message;
    messageBox.style.display = 'block';

    messageBox.classList.remove('success', 'error');

    if (isSuccess) {
        messageBox.classList.add('success');
    } else {
        messageBox.classList.add('error');
    }
}

// --- Form Submission Handler ---
form.addEventListener("submit", async (e) => {
    e.preventDefault();
    changeBtn.disabled = true;
    changeBtn.textContent = 'Processing...';
    messageBox.style.display = 'none';

    const oldPassword = oldPasswordEl.value;
    const newPassword = newPasswordEl.value;
    const confirmPassword = confirmPasswordEl.value;

    // Basic Client-side validation: Check all fields are filled
    if (!oldPassword || !newPassword || !confirmPassword) {
        displayMessage("All fields must be filled out.", false);
        changeBtn.disabled = false;
        changeBtn.textContent = 'Change Password';
        return;
    }

    // User's password comparison logic
    if (newPassword !== confirmPassword) {
        displayMessage("New Password and Confirmed Passwords do not match.", false);
        changeBtn.disabled = false;
        changeBtn.textContent = 'Change Password';
        return;
    }

    // Prevent changing to the same password
    if (oldPassword === newPassword) {
        displayMessage("The new password must be different from the old password.", false);
        changeBtn.disabled = false;
        changeBtn.textContent = 'Change Password';
        return;
    }

    try {
        const res = await post("/change_password", { "old_password": oldPassword, "new_password": newPassword });
        const data = await res.json();

        // Use displayMessage for structured feedback
        if (data.success) {
            displayMessage(data.message, true);
            // Clear fields and redirect on success
            oldPasswordEl.value = '';
            newPasswordEl.value = '';
            confirmPasswordEl.value = '';
            // User's redirection logic
            setTimeout(() => window.location.href = "login.html", 1500);
        } else {
            console.error(data);
            displayMessage(data.message || "An error occurred.", false);
            changeBtn.disabled = false;
            changeBtn.textContent = 'Change Password';
        }

    } catch (err) {
        displayMessage("Error changing passwords. Could not connect to the API.", false);
        console.error(err);
        changeBtn.disabled = false;
        changeBtn.textContent = 'Change Password';
    }
});