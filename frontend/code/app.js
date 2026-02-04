// --- DOM Elements ---
const dispUsername = document.getElementById("disp-username");
const dispName = document.getElementById("disp-name");
const vaultTextarea = document.getElementById("vault");
const dashboardMessage = document.getElementById("dashboard-message");
const saveVaultBtn = document.getElementById("save-vault");
const logoutBtn = document.getElementById("logout");
const adminSectionTitle = document.getElementById("admin-section-title");
const allUsersPre = document.getElementById("all-users");

// --- Load Dashboard ---
async function loadDashboard() {
    dashboardMessage.textContent = "";
    try {
        // --- Get personal information ---
        const res = await get("/get_personal_information");
        const data = await res.json();
        if (data.success) {
            dispUsername.textContent = data.information.username;
            dispName.textContent = `${data.information.first_name} ${data.information.last_name}`;
            vaultTextarea.value = data.information.vault || "";
        } else {
            dashboardMessage.textContent = data.message;
            window.location = "/login.html"; // Redirect if unauthorized
        }

        // --- Get all users if admin ---
        const usersRes = await get("/get_all_users");
        const usersData = await usersRes.json();
        console.log(usersData)
        if (usersData.success) {
            adminSectionTitle.style.display = "block";
            allUsersPre.style.display = "block";
            allUsersPre.textContent = JSON.stringify(usersData.users, null, 2);
        }
    } catch (err) {
        dashboardMessage.textContent = "Error loading dashboard.";
        console.error(err);
    }
}

// --- Save Vault ---
saveVaultBtn.addEventListener("click", async () => {
    try {
        const res = await post("/set_vault_information", { data: vaultTextarea.value });
        const data = await res.json();
        dashboardMessage.textContent = data.message;
    } catch (err) {
        dashboardMessage.textContent = "Error saving vault.";
        console.error(err);
    }
});

// --- Logout ---
logoutBtn.addEventListener("click", async () => {
    try {
        const res = await post("/logout");
        const data = await res.json();
        console.log(data.message);
        window.location.href = "login.html"; // redirect to login
    } catch (err) {
        console.error("Logout failed", err);
        window.location.href = "login.html"; // still redirect
    }
});

// --- Initial Load ---
loadDashboard();