import re

with open('Areas/Identity/Pages/Account/Register.cshtml', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Fix left panel background
content = content.replace(
    '<div class="relative flex flex-col items-end bg-[#0B1F33] px-8 py-14 text-white lg:pl-10 lg:pr-0">',
    '<div class="relative flex flex-col items-end bg-[#0B1F33] px-8 py-14 text-white lg:pl-10 lg:pr-0" style="background-color: #0B1F33;">'
)

# 2. Add Organization name to Billing Summary
old_summary_html = """<!-- Section label -->
                            <p class="text-xs font-semibold uppercase tracking-widest text-white/50 text-center">Billing summary</p>

                            <!-- Plan name -->"""
new_summary_html = """<!-- Section label -->
                            <p class="text-xs font-semibold uppercase tracking-widest text-white/50 text-center">Billing summary</p>

                            <!-- Organization Name -->
                            <h3 class="mt-4 text-center text-lg font-medium text-white/90" id="summaryOrgName"></h3>

                            <!-- Plan name -->"""
content = content.replace(old_summary_html, new_summary_html)

# 3. Update syncPlanSummary to sync organization name
old_sync_js = """function syncPlanSummary() {
            const plan = document.getElementById('selectedPlan')?.value || 'Basic';
            const paymentPlan = document.getElementById('paymentPlan');"""
new_sync_js = """function syncPlanSummary() {
            const plan = document.getElementById('selectedPlan')?.value || 'Basic';
            const orgNameInput = document.getElementById('orgName')?.value?.trim() || 'Your Organization';
            const orgNameEl = document.getElementById('summaryOrgName');
            const paymentPlan = document.getElementById('paymentPlan');
            if (orgNameEl) orgNameEl.textContent = orgNameInput;"""
content = content.replace(old_sync_js, new_sync_js)

# 4. Add dark mode classes to the right panel
# Main container
content = content.replace(
    '<div class="flex flex-col justify-center bg-white px-8 py-14 lg:pl-16 lg:pr-10">',
    '<div class="flex flex-col justify-center bg-white dark:bg-slate-900 px-8 py-14 lg:pl-16 lg:pr-10">'
)

# Headings
content = content.replace('text-2xl font-semibold text-slate-900', 'text-2xl font-semibold text-slate-900 dark:text-white')
content = content.replace('text-sm text-slate-700">Enter your payment information', 'text-sm text-slate-700 dark:text-slate-300">Enter your payment information')

# Payment Method buttons and borders
content = content.replace('border-b border-slate-200', 'border-b border-slate-200 dark:border-slate-700')
content = content.replace('text-xs font-semibold uppercase tracking-widest text-slate-700', 'text-xs font-semibold uppercase tracking-widest text-slate-700 dark:text-slate-300')
content = content.replace('border-slate-900 bg-slate-50', 'border-slate-900 bg-slate-50 dark:border-slate-400 dark:bg-slate-800')
content = content.replace('text-xs font-semibold text-slate-800', 'text-xs font-semibold text-slate-800 dark:text-slate-200')
content = content.replace('border-slate-300 bg-white', 'border-slate-300 bg-white dark:border-slate-600 dark:bg-slate-800')
content = content.replace('text-xs font-semibold text-slate-700', 'text-xs font-semibold text-slate-700 dark:text-slate-300')
content = content.replace('border-2 border-slate-500 bg-white', 'border-2 border-slate-500 bg-white dark:border-slate-600 dark:bg-slate-800 dark:text-white')
content = content.replace('text-sm text-slate-900 placeholder:text-slate-500', 'text-sm text-slate-900 dark:text-white placeholder:text-slate-500 dark:placeholder:text-slate-400')
content = content.replace('bg-slate-50 px-4', 'bg-slate-50 dark:bg-slate-700 px-4')
content = content.replace('text-slate-600">+63</span>', 'text-slate-600 dark:text-slate-300">+63</span>')

with open('Areas/Identity/Pages/Account/Register.cshtml', 'w', encoding='utf-8') as f:
    f.write(content)
