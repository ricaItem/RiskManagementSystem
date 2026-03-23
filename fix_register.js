const fs = require('fs');

let content = fs.readFileSync('Areas/Identity/Pages/Account/Register.cshtml', 'utf-8');

// 1. Fix left panel background
content = content.replace(
    '<div class="relative flex flex-col items-end bg-[#0B1F33] px-8 py-14 text-white lg:pl-10 lg:pr-0">',
    '<div class="relative flex flex-col items-end bg-[#0B1F33] px-8 py-14 text-white lg:pl-10 lg:pr-0" style="background-color: #0B1F33;">'
);

// 2. Add Organization name to Billing Summary
const old_summary_html = `<!-- Section label -->
                            <p class="text-xs font-semibold uppercase tracking-widest text-white/50 text-center">Billing summary</p>

                            <!-- Plan name -->`;
const new_summary_html = `<!-- Section label -->
                            <p class="text-xs font-semibold uppercase tracking-widest text-white/50 text-center">Billing summary</p>

                            <!-- Organization Name -->
                            <h3 class="mt-4 text-center text-lg font-medium text-white/90" id="summaryOrgName"></h3>

                            <!-- Plan name -->`;
content = content.replace(old_summary_html, new_summary_html);

// 3. Update syncPlanSummary to sync organization name
const old_sync_js = `function syncPlanSummary() {
            const plan = document.getElementById('selectedPlan')?.value || 'Basic';
            const paymentPlan = document.getElementById('paymentPlan');`;
const new_sync_js = `function syncPlanSummary() {
            const plan = document.getElementById('selectedPlan')?.value || 'Basic';
            const orgNameInput = document.getElementById('orgName')?.value?.trim() || 'Your Organization';
            const orgNameEl = document.getElementById('summaryOrgName');
            const paymentPlan = document.getElementById('paymentPlan');
            if (orgNameEl) orgNameEl.textContent = orgNameInput;`;
content = content.replace(old_sync_js, new_sync_js);

// 4. Add dark mode classes to the right panel
content = content.replace(
    '<div class="flex flex-col justify-center bg-white px-8 py-14 lg:pl-16 lg:pr-10">',
    '<div class="flex flex-col justify-center bg-white dark:bg-slate-900 px-8 py-14 lg:pl-16 lg:pr-10">'
);
content = content.replace(/text-2xl font-semibold text-slate-900/g, 'text-2xl font-semibold text-slate-900 dark:text-white');
content = content.replace(/text-sm text-slate-700">Enter your payment information/g, 'text-sm text-slate-700 dark:text-slate-300">Enter your payment information');

// Replace borders and texts in the right panel section specifically
// It's safer to just replace globally if it doesn't break other parts
content = content.replace(/border-b border-slate-200/g, 'border-b border-slate-200 dark:border-slate-700');
content = content.replace(/text-xs font-semibold uppercase tracking-widest text-slate-700/g, 'text-xs font-semibold uppercase tracking-widest text-slate-700 dark:text-slate-300');
content = content.replace(/border-slate-900 bg-slate-50/g, 'border-slate-900 bg-slate-50 dark:border-slate-400 dark:bg-slate-800');
content = content.replace(/text-xs font-semibold text-slate-800/g, 'text-xs font-semibold text-slate-800 dark:text-slate-200');
content = content.replace(/border-slate-300 bg-white/g, 'border-slate-300 bg-white dark:border-slate-600 dark:bg-slate-800');
content = content.replace(/text-xs font-semibold text-slate-700/g, 'text-xs font-semibold text-slate-700 dark:text-slate-300');
content = content.replace(/border-2 border-slate-500 bg-white/g, 'border-2 border-slate-500 bg-white dark:border-slate-600 dark:bg-slate-800 dark:text-white');
content = content.replace(/text-sm text-slate-900 placeholder:text-slate-500/g, 'text-sm text-slate-900 dark:text-white placeholder:text-slate-500 dark:placeholder:text-slate-400');
content = content.replace(/bg-slate-50 px-4/g, 'bg-slate-50 dark:bg-slate-700 px-4');
content = content.replace(/text-slate-600">\+63<\/span>/g, 'text-slate-600 dark:text-slate-300">+63</span>');

fs.writeFileSync('Areas/Identity/Pages/Account/Register.cshtml', content, 'utf-8');
console.log("Done");
