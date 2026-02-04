# Quick Start Guide - Tenant List Component

## 🚀 Getting Started in 3 Steps

### Step 1: Navigate to the Tenant List
Open your browser and navigate to:
```
http://localhost:4200/saas/tenants
```

### Step 2: Using the Interface

#### Search for Tenants
1. Type in the search box at the top
2. Results will filter automatically after 500ms
3. Click "Clear Filter" to reset

#### Navigate Pages
- Use the pagination controls at the bottom of the table
- Change items per page: 10, 25, 50, or 100
- Current page information is displayed

#### Sort Data
- Click on the "Tenant Name" column header
- First click: Sort A-Z (ascending)
- Second click: Sort Z-A (descending)
- Third click: Remove sorting

#### Refresh Data
- Click the refresh button (↻) in the top-right corner
- Data will reload without losing your current page/filters

### Step 3: Understanding the Display

**Tenant Name Column:**
- Shows the tenant's display name
- Has a building icon for easy identification

**Tenant ID Column:**
- Unique identifier in monospace font
- Can be copied for API calls or debugging

**Foodics Accounts Column:**
- Green badge: Tenant has Foodics accounts
- Gray badge: No Foodics accounts yet
- Shows count: "1 Account" or "2 Accounts"

**Actions Column:**
- ✏️ Edit: Modify tenant details (coming soon)
- 🗑️ Delete: Remove tenant (coming soon)
- ⋮ More: Additional options (coming soon)

## 📋 Common Scenarios

### Scenario 1: Finding a Specific Tenant
```
1. Type the tenant name or part of it in the search box
2. Wait for results to load (automatic)
3. Click on the tenant to view details (when implemented)
```

### Scenario 2: Viewing All Tenants
```
1. Clear any existing filters
2. Set items per page to your preference
3. Navigate through pages using pagination controls
```

### Scenario 3: Checking Foodics Integration
```
1. Look at the "Foodics Accounts" column
2. Green badges indicate active integrations
3. Gray badges indicate no integration yet
```

## 🔧 Customization for Developers

### Add Menu Link
Add to your navigation menu in `route.provider.ts`:

```typescript
{
  path: '/saas/tenants',
  name: 'Tenants',
  iconClass: 'pi pi-building',
  order: 2,
  layout: eLayoutType.application,
}
```

### Implement Action Buttons
In `tenant-list.component.ts`, add methods:

```typescript
onEdit(tenant: TenantDto): void {
  // Navigate to edit page or open dialog
  this.router.navigate(['/saas/tenants', tenant.id, 'edit']);
}

onDelete(tenant: TenantDto): void {
  // Show confirmation dialog
  this.confirmationService.confirm({
    message: `Are you sure you want to delete ${tenant.name}?`,
    accept: () => {
      this.tenantService.delete(tenant.id).subscribe(() => {
        this.messageService.add({
          severity: 'success',
          summary: 'Success',
          detail: 'Tenant deleted successfully'
        });
        this.refresh();
      });
    }
  });
}
```

Don't forget to wire up the methods in the HTML template:

```html
<p-button 
  icon="pi pi-pencil" 
  (onClick)="onEdit(tenant)" />

<p-button 
  icon="pi pi-trash" 
  (onClick)="onDelete(tenant)" />
```

## 🎨 Styling Tips

### Change Table Appearance
Add to `tenant-list.component.scss`:

```scss
// Striped rows
:host ::ng-deep .p-datatable {
  .p-datatable-tbody > tr:nth-child(odd) {
    background: var(--surface-50);
  }
}

// Hover effect
:host ::ng-deep .p-datatable {
  .p-datatable-tbody > tr:hover {
    background: var(--primary-50) !important;
  }
}
```

### Custom Header Style
```scss
:host ::ng-deep .p-datatable-thead > tr > th {
  background: linear-gradient(180deg, var(--primary-500) 0%, var(--primary-700) 100%);
  color: white;
  font-weight: 600;
}
```

## 🐛 Troubleshooting

### Issue: "No tenants found" but tenants exist
**Solution:**
1. Check browser console for API errors
2. Verify you're logged in
3. Check if you have the correct permissions
4. Clear filters and try again

### Issue: Pagination not working
**Solution:**
1. Verify `totalRecords` is being set correctly
2. Check API response in Network tab
3. Ensure backend returns correct `totalCount`

### Issue: Search returns no results
**Solution:**
1. Verify the API endpoint supports filtering
2. Check the `filter` parameter is being sent
3. Try searching with different terms

## 📱 Mobile Usage

The component is fully responsive:
- **Mobile (< 768px)**: Stacked layout, scrollable table
- **Tablet (768px - 1024px)**: Optimized spacing
- **Desktop (> 1024px)**: Full feature display

## 🔐 Permissions

Required permissions to access this page:
- Authentication required (enforced by `authGuard`)
- Appropriate tenant management permissions

## 🎯 Next Steps

1. **Add Create Functionality**: Implement "New Tenant" button
2. **Add Edit Dialog**: Create modal for editing tenant details
3. **Add Delete Confirmation**: Implement delete with confirmation
4. **Add Bulk Operations**: Select multiple tenants for batch actions
5. **Add Export**: Export tenant list to CSV/Excel

## 📞 Need Help?

- Check the full [README.md](./README.md) for detailed documentation
- Review [PrimeNG Table Documentation](https://primeng.org/table)
- Check [ABP Documentation](https://abp.io/docs)

---

**Pro Tip:** Press `Ctrl + R` to refresh the page data without reloading the entire application!



