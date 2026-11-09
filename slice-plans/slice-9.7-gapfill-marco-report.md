# Slice 9.7 gap-fill — Admin categories screen — Marco report

Concerns: Edit-field labels `Category name` / `Category slug` (plan D5) equal the create form's labels on the same screen, so tests pick the open editor's field with `getAllByLabelText(...).at(-1)`. Screen readers hear duplicate labels; a distinct label (e.g. `Edit category name`) would be cleaner. Not changed (plan-specified).
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: no existing test weakened or deleted. Additions only: AdminProductListView.test.tsx (+1: Categories link, no Add category button; there was no prior test of the form there, so nothing was moved); DashboardView.test.tsx (`Categories` added to the quick-actions loop); admin-access.test.ts (`/admin/categories` added to PATHS: Support -> `/admin`, Admin -> null, none/Customer redirects); routes.test.tsx (+1 row); api.test.ts (+2 cases: A1-A3).
Red run: AdminCategoriesView.test.tsx written first; failed with "Cannot find module './AdminCategoriesView'" (2 projects), then green once CategoryRow/AdminCategoriesView existed.

Changed (frontend/apps/store/):
- New: app/admin/categories.tsx; features/admin-catalog/components/{AdminCategoriesView,CategoryRow}.tsx, AdminCategoriesView.test.tsx; hooks/{useUpdateCategoryMutation,useDeleteCategoryMutation}.ts
- Edited: features/admin-catalog/{api.ts,types.ts,api.test.ts}; components/AdminProductListView.tsx (+test); features/admin-dashboard/components/DashboardView.tsx (+test); features/auth/admin-access.test.ts; tests/routes.test.tsx
- CategoryCreateForm.tsx and its test unchanged. No backend, package or doc change.

Deviations: none from D1-D9. Mutations: none (Low tier). Update invalidates categories, admin products and products on settle; delete invalidates categories on settle. PUT body is exactly { slug, name } (trimmed).

verify.sh:
VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)
build 0 Error(s)/0 Warning(s); jest: Test Suites: 161 passed, Tests: 1137 passed; tsc clean; export ok.

Quality-bar §2: tests for new behavior ✔; no secrets ✔; no new package ✔; touch targets via ActionButton ✔; role/label assertions, no class assertions ✔; backend/docs untouched ✔; no stray processes (only jest/verify, finished) ✔. Mutation/§11.2 items N/A (Low tier, none named).

## Fix round 1
- Blocker 1: CategoryRow save/confirmDelete now use `mutateAsync(...).then(onClose, () => undefined)`. New test "does not reopen the row when a slug comes back after a delete/rename" (it.each, GET mock drops then restores `tops`). It did NOT go red before the fix: the per-call callback fires before React commits the unmount (notifyManager batches via setTimeout), so I could not reproduce Rob's scenario in Jest. Fix applied as instructed; the test guards the behaviour.
- Blocker 2: edit labels are `New name for <name>` / `New slug for <name>`; test helpers use getByLabelText; no `.at(-1)` remains.
- Tests changed: nameField/slugField helpers (new labels, no weakening); added the it.each reopen test.
