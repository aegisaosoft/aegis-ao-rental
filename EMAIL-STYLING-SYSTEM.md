# Email Styling System for Rental Companies

This document describes the comprehensive email styling system that allows rental companies to customize their email templates with their own branding, colors, and styling.

## Overview

The Email Styling System provides:
- **Company-specific email templates** with custom branding
- **Color scheme customization** for all email elements
- **Logo and footer customization** for brand consistency
- **Responsive email templates** that work on all devices
- **Preview functionality** to see how emails will look
- **Multiple email types** (booking links, confirmations, payments, etc.)

## Features

### 🎨 **Visual Customization**
- **Primary Colors** - Main brand colors for headers and buttons
- **Secondary Colors** - Supporting colors for accents
- **Status Colors** - Success, warning, info, and error colors
- **Background Colors** - Content backgrounds and sections
- **Text Colors** - Readable text with proper contrast
- **Border Colors** - Subtle borders and dividers

### 🖼️ **Branding Elements**
- **Company Logo** - Custom logo display in email headers
- **Footer Text** - Custom footer messages and legal text
- **Font Family** - Custom fonts for brand consistency
- **Color Schemes** - Complete color palette customization

### 📧 **Email Types Supported**
- **Booking Link Emails** - Secure booking links with vehicle details
- **Booking Confirmations** - Confirmation emails with booking details
- **Payment Success** - Payment confirmation notifications
- **Welcome Emails** - Customer onboarding emails
- **Reminder Emails** - Booking expiration reminders

## Database Schema

### Company Email Styles Table

```sql
CREATE TABLE company_email_styles (
    style_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    primary_color VARCHAR(7) DEFAULT '#007bff',
    secondary_color VARCHAR(7) DEFAULT '#6c757d',
    success_color VARCHAR(7) DEFAULT '#28a745',
    warning_color VARCHAR(7) DEFAULT '#ffc107',
    info_color VARCHAR(7) DEFAULT '#17a2b8',
    background_color VARCHAR(7) DEFAULT '#f8f9fa',
    border_color VARCHAR(7) DEFAULT '#dee2e6',
    text_color VARCHAR(7) DEFAULT '#333333',
    header_text_color VARCHAR(7) DEFAULT '#ffffff',
    button_text_color VARCHAR(7) DEFAULT '#ffffff',
    footer_color VARCHAR(7) DEFAULT '#343a40',
    footer_text_color VARCHAR(7) DEFAULT '#ffffff',
    logo_url VARCHAR(500),
    footer_text VARCHAR(500),
    font_family VARCHAR(100) DEFAULT 'Arial, sans-serif',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## API Endpoints

### 1. Get Company Email Style

**GET** `/api/companyemailstyle/company/{companyId}`

Retrieve the email style configuration for a company.

#### Response

```json
{
  "styleId": "uuid",
  "companyId": "uuid",
  "primaryColor": "#007bff",
  "secondaryColor": "#6c757d",
  "successColor": "#28a745",
  "warningColor": "#ffc107",
  "infoColor": "#17a2b8",
  "backgroundColor": "#f8f9fa",
  "borderColor": "#dee2e6",
  "textColor": "#333333",
  "headerTextColor": "#ffffff",
  "buttonTextColor": "#ffffff",
  "footerColor": "#343a40",
  "footerTextColor": "#ffffff",
  "logoUrl": "https://example.com/logo.png",
  "footerText": "© 2025 Premium Rentals Inc. All rights reserved.",
  "fontFamily": "Arial, sans-serif",
  "isActive": true,
  "createdAt": "2025-01-15T10:00:00Z",
  "updatedAt": "2025-01-15T10:00:00Z"
}
```

### 2. Create or Update Email Style

**POST** `/api/companyemailstyle/company/{companyId}`

Create or update email style configuration for a company.

#### Request Body

```json
{
  "primaryColor": "#1a365d",
  "secondaryColor": "#4a5568",
  "successColor": "#38a169",
  "warningColor": "#d69e2e",
  "infoColor": "#3182ce",
  "backgroundColor": "#f7fafc",
  "borderColor": "#e2e8f0",
  "textColor": "#2d3748",
  "headerTextColor": "#ffffff",
  "buttonTextColor": "#ffffff",
  "footerColor": "#2d3748",
  "footerTextColor": "#ffffff",
  "logoUrl": "https://premiumrentals.com/logo.png",
  "footerText": "© 2025 Premium Rentals Inc. All rights reserved.",
  "fontFamily": "Inter, sans-serif"
}
```

### 3. Update Email Style

**PUT** `/api/companyemailstyle/company/{companyId}`

Update specific email style properties.

#### Request Body

```json
{
  "primaryColor": "#2b6cb0",
  "logoUrl": "https://newlogo.com/logo.png",
  "footerText": "Updated footer text"
}
```

### 4. Delete Email Style

**DELETE** `/api/companyemailstyle/company/{companyId}`

Deactivate email style for a company.

### 5. Get Email Style Preview

**GET** `/api/companyemailstyle/preview/{companyId}`

Get a preview of how emails will look with the current styling.

#### Response

```json
{
  "companyId": "uuid",
  "primaryColor": "#007bff",
  "secondaryColor": "#6c757d",
  "successColor": "#28a745",
  "warningColor": "#ffc107",
  "infoColor": "#17a2b8",
  "backgroundColor": "#f8f9fa",
  "borderColor": "#dee2e6",
  "textColor": "#333333",
  "headerTextColor": "#ffffff",
  "buttonTextColor": "#ffffff",
  "footerColor": "#343a40",
  "footerTextColor": "#ffffff",
  "logoUrl": "https://example.com/logo.png",
  "footerText": "© 2025 Car Rental System. All rights reserved.",
  "fontFamily": "Arial, sans-serif",
  "previewHtml": "<!DOCTYPE html>..."
}
```

## Email Template Features

### 🎨 **Professional Design**
- **Responsive Layout** - Works on desktop, tablet, and mobile
- **Professional Typography** - Clean, readable fonts
- **Consistent Branding** - Company colors and logos throughout
- **Clear Call-to-Actions** - Prominent buttons and links
- **Visual Hierarchy** - Well-organized content structure

### 📱 **Mobile Responsive**
- **Flexible Layout** - Adapts to different screen sizes
- **Touch-Friendly** - Large buttons and touch targets
- **Readable Text** - Appropriate font sizes for mobile
- **Optimized Images** - Properly sized logos and images

### 🎯 **Email Types**

#### **Booking Link Email**
- **Secure booking link** with company branding
- **Vehicle details** with images and features
- **Pricing breakdown** with clear totals
- **Expiration notice** with urgency messaging
- **Company contact** information

#### **Booking Confirmation Email**
- **Confirmation number** prominently displayed
- **Complete booking details** with all information
- **Next steps** for customer guidance
- **Company contact** for support
- **Professional confirmation** styling

#### **Payment Success Email**
- **Payment confirmation** with amount details
- **Vehicle information** for reference
- **Next steps** for pickup process
- **Company branding** throughout
- **Professional receipt** styling

#### **Welcome Email**
- **Company introduction** and branding
- **Service features** and benefits
- **Call-to-action** for first booking
- **Company contact** information
- **Professional welcome** message

#### **Reminder Email**
- **Urgency messaging** for expiring bookings
- **Booking summary** with key details
- **Clear call-to-action** to complete booking
- **Expiration countdown** for urgency
- **Company branding** and contact info

## Color Customization

### **Primary Colors**
- **Headers and main buttons** - Company's main brand color
- **Call-to-action elements** - Primary brand color
- **Important highlights** - Brand color for emphasis

### **Secondary Colors**
- **Supporting elements** - Complementary brand colors
- **Accent details** - Secondary brand elements
- **Subtle highlights** - Supporting color scheme

### **Status Colors**
- **Success (Green)** - Payment confirmations, completed actions
- **Warning (Yellow/Orange)** - Expiring bookings, important notices
- **Info (Blue)** - General information, tips
- **Error (Red)** - Failed actions, problems

### **Background Colors**
- **Content backgrounds** - Light, readable backgrounds
- **Section backgrounds** - Subtle section separation
- **Card backgrounds** - Clean content containers

### **Text Colors**
- **Primary text** - Dark, readable text
- **Secondary text** - Lighter supporting text
- **Header text** - Contrasting header text
- **Link text** - Branded link colors

## Logo and Branding

### **Company Logo**
- **Header placement** - Prominent logo in email header
- **Responsive sizing** - Scales appropriately on all devices
- **High quality** - Sharp, professional logo images
- **Brand consistency** - Matches company website and materials

### **Footer Customization**
- **Company information** - Name, contact details
- **Legal text** - Copyright, terms, privacy
- **Social links** - Company social media presence
- **Unsubscribe options** - Compliance with email regulations

## Font Customization

### **Font Families**
- **Web-safe fonts** - Arial, Helvetica, Georgia, Times
- **Google Fonts** - Inter, Roboto, Open Sans, Lato
- **Custom fonts** - Company-specific font choices
- **Fallback fonts** - Graceful degradation for compatibility

### **Typography Hierarchy**
- **Headers** - Bold, prominent headings
- **Body text** - Readable, comfortable line height
- **Links** - Clearly identifiable and clickable
- **Buttons** - Bold, action-oriented text

## Implementation Examples

### **Setting Up Company Email Style**

```bash
# Create email style for a company
curl -X POST "https://api.carrental.com/api/companyemailstyle/company/{company-id}" \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "primaryColor": "#1a365d",
    "successColor": "#38a169",
    "logoUrl": "https://company.com/logo.png",
    "footerText": "© 2025 Company Name. All rights reserved.",
    "fontFamily": "Inter, sans-serif"
  }'
```

### **Updating Email Style**

```bash
# Update specific style properties
curl -X PUT "https://api.carrental.com/api/companyemailstyle/company/{company-id}" \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "primaryColor": "#2b6cb0",
    "logoUrl": "https://newlogo.com/logo.png"
  }'
```

### **Preview Email Style**

```bash
# Get email style preview
curl -X GET "https://api.carrental.com/api/companyemailstyle/preview/{company-id}" \
  -H "Authorization: Bearer <jwt-token>"
```

## Best Practices

### **Color Selection**
- **High contrast** - Ensure text is readable on backgrounds
- **Brand consistency** - Use company's official brand colors
- **Accessibility** - Consider colorblind users and accessibility
- **Professional appearance** - Avoid overly bright or distracting colors

### **Logo Guidelines**
- **High resolution** - Use high-quality logo images
- **Appropriate size** - Not too large or too small
- **Consistent branding** - Match company's visual identity
- **Web optimization** - Optimize file size for email delivery

### **Typography**
- **Readable fonts** - Choose fonts that are easy to read
- **Consistent hierarchy** - Use consistent heading and text styles
- **Appropriate sizing** - Ensure text is readable on all devices
- **Brand alignment** - Use fonts that match company branding

### **Content Structure**
- **Clear hierarchy** - Organize content logically
- **Scannable layout** - Make it easy to find important information
- **Call-to-actions** - Make buttons and links prominent
- **Mobile optimization** - Ensure content works on mobile devices

## Technical Implementation

### **Template Engine**
- **Razor templates** - Server-side template rendering
- **Dynamic content** - Company-specific content injection
- **Style variables** - CSS variables for easy customization
- **Responsive design** - Mobile-first responsive templates

### **Email Delivery**
- **SMTP integration** - Reliable email delivery
- **Template caching** - Performance optimization
- **Error handling** - Graceful failure handling
- **Delivery tracking** - Email delivery status monitoring

### **Database Integration**
- **Style persistence** - Company styles stored in database
- **Version control** - Track style changes over time
- **Default fallbacks** - Graceful degradation for missing styles
- **Performance optimization** - Efficient style retrieval

This email styling system provides rental companies with complete control over their email branding, ensuring consistent, professional communication with customers while maintaining the flexibility to customize their visual identity.
