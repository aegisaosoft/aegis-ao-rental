# Video/Media Upload Feature

## 📹 Overview
Added functionality to upload and manage company videos, banners, and logos with automatic folder organization.

## 🗂️ File Organization Structure
```
/uploads/<YYYY-MM-DD>/<company-id>/
├── videos/    (max 500 MB per file)
├── banners/   (max 10 MB per file)
└── logos/     (max 5 MB per file)
```

## ✨ Features

### 1. **Video Upload**
- **Supported formats:** MP4, AVI, MOV, WMV, WebM, MKV
- **Max size:** 500 MB
- **Upload progress:** Real-time progress bar
- **Preview:** Video player with controls
- **Delete:** One-click deletion with confirmation

### 2. **Banner Upload**
- **Supported formats:** JPG, PNG, GIF, WebP
- **Max size:** 10 MB
- **Upload progress:** Real-time progress bar
- **Preview:** Image thumbnail
- **Delete:** One-click deletion with confirmation

### 3. **Logo Upload**
- **Supported formats:** JPG, PNG, SVG, WebP
- **Max size:** 5 MB
- **Upload progress:** Real-time progress bar
- **Preview:** Image thumbnail
- **Delete:** One-click deletion with confirmation

## 🔧 Backend (API)

### New Controller: `MediaController.cs`
Located at: `CarRental.Api/Controllers/MediaController.cs`

#### Endpoints:

**Upload Video:**
```
POST /api/Media/companies/{companyId}/video
Content-Type: multipart/form-data
Body: { video: File }
```

**Delete Video:**
```
DELETE /api/Media/companies/{companyId}/video
```

**Upload Banner:**
```
POST /api/Media/companies/{companyId}/banner
Content-Type: multipart/form-data
Body: { banner: File }
```

**Delete Banner:**
```
DELETE /api/Media/companies/{companyId}/banner
```

**Upload Logo:**
```
POST /api/Media/companies/{companyId}/logo
Content-Type: multipart/form-data
Body: { logo: File }
```

**Delete Logo:**
```
DELETE /api/Media/companies/{companyId}/logo
```

### File Storage
- Files are stored in `wwwroot/uploads/`
- Static file serving enabled in `Program.cs`
- Old files automatically deleted on new upload

### Configuration Changes

**Program.cs:**
- Added static file serving: `app.UseStaticFiles()`
- Configured file upload limits (500 MB max)
```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
    options.ValueLengthLimit = 524_288_000;
    options.MultipartHeadersLengthLimit = 524_288_000;
});
```

## 💻 Frontend (React)

### Updated Files

**1. `client/src/services/api.js`**
Added new methods:
- `uploadCompanyVideo(companyId, file, onProgress)`
- `deleteCompanyVideo(companyId)`
- `uploadCompanyBanner(companyId, file, onProgress)`
- `deleteCompanyBanner(companyId)`
- `uploadCompanyLogo(companyId, file, onProgress)`
- `deleteCompanyLogo(companyId)`

**2. `client/src/pages/AdminDashboard.js`**
Added:
- Upload progress state tracking
- File upload handlers with validation
- File delete handlers with confirmation
- UI components for file upload/preview/delete

### UI Components

**Upload Interface:**
- Clean file input button
- Progress bar during upload
- Preview when file exists
- Delete button

**Example UI:**
```jsx
{companyFormData.videoLink ? (
  <div>
    <video src={companyFormData.videoLink} controls />
    <button onClick={handleVideoDelete}>Delete</button>
  </div>
) : (
  <div>
    <input type="file" onChange={handleVideoUpload} />
    {isUploading.video && <ProgressBar progress={uploadProgress.video} />}
  </div>
)}
```

## 🔒 Security Features

### File Validation
1. **File Type:** Only allowed extensions accepted
2. **File Size:** Size limits enforced (500 MB for videos, 10 MB for banners, 5 MB for logos)
3. **MIME Type:** Server-side validation of content type

### Error Handling
- Client-side validation before upload
- Server-side validation
- User-friendly error messages
- Automatic cleanup on failure

## 📊 Database Integration

The uploaded file URLs are automatically saved to the `rental_companies` table:
- `video_link` - Path to uploaded video
- `banner_link` - Path to uploaded banner
- `logo_link` - Path to uploaded logo

## 🚀 How to Use

### For Users:
1. Navigate to **Admin Dashboard**
2. Click **Company Profile** section
3. Under **Media Links**:
   - Click "Choose File" for Logo/Banner/Video
   - Select your file
   - Wait for upload (progress bar shows status)
   - Preview appears when upload completes
   - Click "Delete" to remove a file

### For Developers:

**Restart API:**
```powershell
cd C:\aegis-ao\rental\aegis-ao-rental\CarRental.Api
dotnet run
```

**Frontend already updated** - just refresh the browser

## ✅ Testing Checklist

- [ ] Upload a video (< 500 MB)
- [ ] Upload a banner image (< 10 MB)
- [ ] Upload a logo (< 5 MB)
- [ ] Verify file appears in `/uploads/<date>/<company-id>/`
- [ ] Verify preview shows correctly
- [ ] Delete each file and verify removal
- [ ] Test file size limits
- [ ] Test invalid file types
- [ ] Test upload progress indicator

## 📝 Notes

- Videos are served directly from the API server
- Files are organized by upload date for easy management
- Old files are automatically deleted when new ones are uploaded
- Files persist across API restarts
- Static file serving is required (already configured)

## 🔄 Upgrade Path

If you need to move files to cloud storage (Azure Blob, AWS S3):
1. Update `MediaController` upload methods
2. Change file storage from local filesystem to cloud service
3. Update URL generation to return cloud URLs
4. Frontend code remains unchanged

## 🐛 Troubleshooting

**Problem:** Upload fails with "File too large"
**Solution:** Check file size limits in MediaController

**Problem:** Video doesn't play
**Solution:** Ensure browser supports video format, try converting to MP4

**Problem:** Files not accessible
**Solution:** Verify `app.UseStaticFiles()` is in Program.cs

**Problem:** Upload progress stuck at 0%
**Solution:** Check API endpoint is receiving the file

