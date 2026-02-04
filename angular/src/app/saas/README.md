# Tenant List Component

A comprehensive tenant management component built with PrimeNG Table and ABP Framework.

## Overview

This component provides a fully-featured tenant list interface with server-side pagination, sorting, filtering, and a modern UI using PrimeNG and Tailwind CSS.

## Features

### ✨ Core Functionality
- **Server-side Pagination**: Efficient data loading with configurable page sizes (10, 25, 50, 100 items)
- **Lazy Loading**: Data is fetched on-demand as users navigate through pages
- **Column Sorting**: Click on table headers to sort by tenant name (ascending/descending)
- **Search/Filter**: Real-time search with 500ms debounce for optimal performance
- **Loading States**: Skeleton loaders during data fetching for better UX
- **Empty States**: Helpful messages when no data is available

### 🎨 UI/UX Features
- **Responsive Design**: Mobile-friendly layout using Tailwind CSS
- **Dark Mode Support**: Automatic theme switching based on app settings
- **Modern Card Layout**: Clean, organized interface following Sakai template patterns
- **Action Buttons**: Edit, Delete, and More options for each tenant
- **Visual Indicators**: 
  - Building icon for tenant names
  - Monospace font for tenant IDs
  - Color-coded tags for Foodics account counts
- **Tooltips**: Helpful hints on hover for all action buttons

### 📊 Data Display
- **Tenant Name**: With building icon for visual recognition
- **Tenant ID**: Displayed in monospace code style
- **Foodics Accounts**: Badge showing account count with color coding
  - Green (success) when accounts exist
  - Gray (secondary) when no accounts
- **Actions Column**: Edit, Delete, and More options buttons

## Component Architecture

### File Structure
```
src/app/saas/tenant-list.component/
├── tenant-list.component.ts        # Component logic
├── tenant-list.component.html      # Template
└── tenant-list.component.scss      # Styles (currently empty, using Tailwind)
```

### Key Dependencies

#### PrimeNG Modules
- `TableModule` - Core table functionality with pagination and sorting
- `ButtonModule` - Action buttons throughout the UI
- `InputTextModule` - Search input field
- `IconFieldModule` & `InputIconModule` - Search icon integration
- `ToastModule` - Toast notifications for errors/success messages
- `TagModule` - Foodics account count badges
- `SkeletonModule` - Loading state animations
- `TooltipModule` - Hover tooltips for buttons

#### ABP Framework
- `TenantService` - API service for tenant operations
- `PagedResultDto<TenantDto>` - Typed response model
- `GetTenantsInput` - Request parameters interface

## Usage

### Accessing the Component

The component is available at the route: `/saas/tenants`

It's protected by:
- `authGuard` - User must be authenticated
- `permissionGuard` - User must have appropriate permissions

### Component Methods

#### `loadTenants(event?: TableLazyLoadEvent)`
Loads tenants from the API with pagination, sorting, and filtering.

**Parameters:**
- `event.first` - Index of the first record (for pagination)
- `event.rows` - Number of rows per page
- `event.sortField` - Field to sort by
- `event.sortOrder` - Sort direction (1 for asc, -1 for desc)

**Behavior:**
- Constructs `GetTenantsInput` with pagination and filter parameters
- Calls `tenantService.getList()`
- Updates `tenants` array and `totalRecords`
- Handles errors with toast notifications

#### `onFilter(event: Event)`
Handles search input with 500ms debounce.

**Behavior:**
- Extracts filter value from input event
- Debounces API calls to avoid excessive requests
- Resets pagination to first page
- Triggers `loadTenants()` with new filter

#### `clearFilter()`
Clears the current filter and reloads data.

#### `refresh()`
Reloads current page without resetting pagination.

#### `getFoodicsAccountCount(tenant: TenantDto)`
Utility method to safely get the count of Foodics accounts.

## API Integration

### Endpoint
The component uses the ABP tenant management API:
- **Base URL**: `/api/multi-tenancy/tenants`
- **Method**: GET
- **API Name**: `AbpTenantManagement`

### Request Parameters (`GetTenantsInput`)
```typescript
{
  filter?: string;           // Search text
  sorting?: string;          // e.g., "name asc" or "name desc"
  skipCount: number;         // Pagination offset
  maxResultCount: number;    // Page size
}
```

### Response (`PagedResultDto<TenantDto>`)
```typescript
{
  items: TenantDto[];        // Array of tenant data
  totalCount: number;        // Total records for pagination
}
```

### TenantDto Model
```typescript
{
  id: string;                    // Unique tenant identifier
  name: string;                  // Tenant name
  concurrencyStamp?: string;     // For optimistic concurrency
  foodicsAccounts: FoodicsAccountDto[];  // Associated Foodics accounts
}
```

## Customization Guide

### Changing Page Size Options
Edit the `rowsPerPageOptions` in the template:

```html
<p-table 
  [rowsPerPageOptions]="[10, 25, 50, 100]"
  ...>
```

### Adding New Columns
1. Add column header in the `<ng-template pTemplate="header">` section
2. Add column data in the `<ng-template pTemplate="body">` section
3. Update the loading skeleton
4. Update the empty message colspan

### Customizing Search Debounce Time
Change the timeout value in `onFilter()`:

```typescript
setTimeout(() => {
  // ... search logic
}, 500); // Change this value (in milliseconds)
```

### Styling Customization
Add custom styles to `tenant-list.component.scss`:

```scss
// Example: Custom table header styling
:host ::ng-deep {
  .p-datatable thead th {
    background-color: var(--primary-color);
    color: white;
  }
}
```

## Error Handling

The component includes comprehensive error handling:

- **API Errors**: Displayed via toast notifications
- **Network Failures**: Gracefully handled with error messages
- **Empty Results**: User-friendly empty state messages
- **Loading States**: Visual feedback during data fetching

## Performance Optimizations

1. **Lazy Loading**: Only loads data for the current page
2. **Debounced Search**: Prevents excessive API calls during typing
3. **Skeleton Loaders**: Improves perceived performance
4. **Efficient Change Detection**: Uses OnPush strategy implicitly through standalone components

## Best Practices

1. **Always handle errors**: Display user-friendly messages
2. **Provide loading feedback**: Use skeleton loaders or spinners
3. **Implement debouncing**: For search and filter operations
4. **Server-side operations**: Pagination, sorting, and filtering for large datasets
5. **Responsive design**: Ensure mobile compatibility
6. **Accessibility**: Use semantic HTML and ARIA labels where needed

## Future Enhancements

Potential improvements for this component:

- [ ] Add tenant creation dialog
- [ ] Implement edit functionality
- [ ] Add delete confirmation dialog
- [ ] Export to CSV/Excel
- [ ] Advanced filtering (date ranges, status, etc.)
- [ ] Multi-select with bulk operations
- [ ] Column visibility toggle
- [ ] Column reordering
- [ ] Row expansion for detailed view
- [ ] Integration with ABP permission system for action buttons

## Troubleshooting

### Table not loading data
- Verify the API endpoint is accessible
- Check browser console for errors
- Ensure `TenantService` is properly injected
- Verify authentication and permissions

### Pagination not working
- Ensure `[lazy]="true"` is set on p-table
- Verify `totalRecords` is being set correctly
- Check that `(onLazyLoad)` event is bound

### Search not filtering
- Verify the API supports the `filter` parameter
- Check that debounce logic is working
- Ensure filter text is being passed to the API

## Related Documentation

- [PrimeNG Table Documentation](https://primeng.org/table)
- [ABP Multi-Tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy)
- [Sakai Template](https://sakai.primeng.org/)

## Support

For issues or questions:
1. Check the browser console for errors
2. Verify API responses in Network tab
3. Review ABP logs for backend issues
4. Consult PrimeNG documentation for UI issues



