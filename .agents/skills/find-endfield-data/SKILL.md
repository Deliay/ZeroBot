---
name: find-endfield-data
description: "Access and retrieve game data structures and assets from 'Arknights: Endfield'. USE FOR: Research game data tables, retrieve configuration data, search and download textures/images, explore asset bundles."
---

# Find Endfield Data

This skill provides access to game data structures and assets from 'Arknights: Endfield'. Use it to research game configuration, retrieve data tables, and search/download game textures and images.

## When to Use This Skill

Use this skill when you need to:
- Research game data structures and configuration
- Retrieve specific data values from game tables
- Search for localized text content in game data tables
- Search for game assets (textures, images, sprites)
- Download game resources for analysis or development

## Data Tables

The game's `Table` data structures are dictionaries with `{ [key: string]: any }` format.

### Get All Tables

Retrieve a list of all available data tables.

- **Endpoint:** `https://endfield-assets.fffdan.com/vfs/Table/`
- **Returns:** `string[]` - Array of table file paths
- **Note:** Returns paths like `Data/TableCfg/{FileName}.bytes` where `{FileName}` is the table name

### Get Table Keys

Retrieve all keys for a specific table.

- **Endpoint:** `https://endfield-assets.fffdan.com/table/{TableName}`
- **Returns:** `string[]` - Array of key names
- **Parameters:** `{TableName}` - Name of the table to query

### Get Table Value

Retrieve a specific value from a table by key.

- **Endpoint:** `https://endfield-assets.fffdan.com/table/{TableName}/{Key}`
- **Returns:** `any` - The value associated with the key
- **Parameters:** 
  - `{TableName}` - Name of the table
  - `{Key}` - Key to retrieve

### Get All Table Values

Retrieve all key-value pairs from a table.

- **Endpoint:** `https://endfield-assets.fffdan.com/table/{TableName}/all`
- **Returns:** `{ [key: string]: any }` - Complete table data
- **Warning:** This endpoint returns large amounts of data and may exceed context limits. Use with caution.

## I18n Search

Search for localized text content in game data tables using regular expressions.

### Search I18n Text

Search for localized text content across game data tables using regex patterns.

- **Endpoint:** `https://endfield-assets.fffdan.com/i18n/search/{table}/{regex}`
- **Returns:** Array of matching text entries with their locations
- **Parameters:**
  - `{table}` - Table name to search in (same as `/vfs/Table/` table names) or `all` to search across all tables
  - `{regex}` - Regular expression pattern to match against text content
- **Example:** `https://endfield-assets.fffdan.com/i18n/search/CharacterTable/治疗.*范围` searches for text containing "治疗" followed by "范围" in CharacterTable
- **Example:** `https://endfield-assets.fffdan.com/i18n/search/all/攻击.*提升` searches for text containing "攻击" followed by "提升" across all tables

### Get I18n Text

Retrieve a specific localized text entry by locale and ID.

- **Endpoint:** `https://endfield-assets.fffdan.com/i18n/{locale}/{id}`
- **Returns:** `string` - The localized text string
- **Parameters:**
  - `{locale}` - Language code (`CN`, `EN`, or `JP`)
  - `{id}` - Numeric ID (corresponds to the `id` field in `{ id: number, text: string }` data)
- **Example:** `https://endfield-assets.fffdan.com/i18n/EN/1001` returns the English text with ID 1001

## Game Assets

Search and download game textures, images, and other assets.

### Search Assets

Search for game assets by query string.

- **Endpoint:** `https://endfield-assets.fffdan.com/vfs/Bundle/search/{*query}`
- **Returns:** `string[]` - Array of matching asset paths
- **Parameters:** 
  - `{*query}` - Search query string
  - Optional: `suffix={suffix}` - Filter by file extension
- **Example:** `https://endfield-assets.fffdan.com/vfs/Bundle/search/ui/sprites?suffix=.png`

### Downloadable Assets

After searching, you can download assets by constructing the full URL.

**Base URL:** `https://endfield-assets.fffdan.com/vfs/Bundle/file/`

**Example:**
- Search result: `assets/beyond/initialassets/ui/sprites/login/login_test_deco.png`
- Download URL: `https://endfield-assets.fffdan.com/vfs/Bundle/file/assets/beyond/initialassets/ui/sprites/login/login_test_deco.png`

**Supported formats:** `.png`, `.tga`
- Other file types will return `404` when downloaded

## Usage Examples

### Example 1: Get all available tables
```
GET https://endfield-assets.fffdan.com/vfs/Table/
```
Returns a list of all table file paths like `["Data/TableCfg/CharacterTable.bytes", "Data/TableCfg/ItemTable.bytes", ...]`

### Example 2: Get keys for a specific table
```
GET https://endfield-assets.fffdan.com/table/CharacterTable
```
Returns all character IDs like `["char_001", "char_002", ...]`

### Example 3: Get a specific character's data
```
GET https://endfield-assets.fffdan.com/table/CharacterTable/char_001
```
Returns the complete data for character `char_001`

### Example 4: Search for UI assets
```
GET https://endfield-assets.fffdan.com/vfs/Bundle/search/ui/sprites?suffix=.png
```
Returns all PNG files in the UI sprites directory

### Example 5: Download a specific texture
```
GET https://endfield-assets.fffdan.com/vfs/Bundle/file/assets/beyond/initialassets/ui/sprites/login/login_test_deco.png
```
Downloads the specified texture file

### Example 6: Search for localized text in a specific table
```
GET https://endfield-assets.fffdan.com/i18n/search/CharacterTable/治疗.*范围
```
Searches for text containing "治疗" followed by "范围" in CharacterTable

### Example 7: Search for localized text across all tables
```
GET https://endfield-assets.fffdan.com/i18n/search/all/攻击.*提升
```
Searches for text containing "攻击" followed by "提升" across all available tables

### Example 8: Get localized text by ID
```
GET https://endfield-assets.fffdan.com/i18n/EN/1001
```
Returns the English text string for ID 1001, e.g. `"Hello"`

## Best Practices

1. **Start with table discovery** - Use `/vfs/Table/` to see what data is available
2. **Use specific queries** - When searching assets, be specific to avoid large result sets
3. **Handle large data carefully** - The `/table/{TableName}/all` endpoint returns massive amounts of data
4. **Check file formats** - Only `.png` and `.tga` files are downloadable
5. **Respect rate limits** - Avoid making too many requests in quick succession
6. **Use regex wisely** - When using `/i18n/search/`, craft precise regex patterns to avoid excessive results

## Common Issues

1. **404 errors on download** - Only `.png` and `.tga` files are supported for download
2. **Large context usage** - Use `/table/{TableName}/all` sparingly as it can exceed context limits
3. **Missing table data** - Some tables may be empty or not yet available in the API

