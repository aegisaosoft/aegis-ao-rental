# Aegis-AO Rental API

A comprehensive car rental management system with multi-company support, Stripe integration, and complete rental workflow management.

## Features

- **Multi-Company Support**: Manage multiple rental companies with individual settings
- **Customer Management**: Complete customer registration, verification, and payment methods
- **Vehicle Inventory**: Vehicle management with categories, status tracking, and availability
- **Reservation System**: Complete reservation workflow from creation to completion
- **Rental Tracking**: Active rental management with mileage and damage tracking
- **Payment Processing**: Stripe integration for secure payment processing
- **Review System**: Customer reviews and ratings for vehicles and companies
- **Analytics & Reporting**: Comprehensive statistics and reporting

## Technology Stack

- **.NET 9.0** - Latest .NET framework
- **ASP.NET Core Web API** - RESTful API framework
- **Entity Framework Core** - ORM for database operations
- **PostgreSQL** - Primary database
- **JWT Authentication** - Secure token-based authentication
- **Stripe Integration** - Payment processing
- **Swagger/OpenAPI** - API documentation

## User Roles and Permissions

The system supports two types of users:

### Customer Users
- **Role**: `user`
- **Access**: Can browse vehicles, make reservations, manage their profile
- **Authentication**: Via `/api/Auth/login` endpoint

### System Users
- **Worker**: Basic system access, company-specific operations
- **Admin**: Full company management, can manage workers
- **MainAdmin**: System-wide access, can manage all companies and users
- **Authentication**: Via `/api/Auth/login-user` endpoint

### Role-Based Access Control
- **Workers**: Limited to their assigned company
- **Admins**: Can manage their company and its workers
- **MainAdmins**: Full system access, can manage all entities

## Database Schema

The system uses a comprehensive PostgreSQL schema with:
- Multi-company support with Stripe Connect integration
- UUID primary keys for all entities
- Complete audit trails with timestamps
- Status tracking for reservations, rentals, and payments
- Review and rating system
- Payment method management

## API Endpoints

### Authentication
- `POST /api/Auth/register` - Customer registration
- `POST /api/Auth/login` - Customer login
- `POST /api/Auth/login-user` - System user login (worker, admin, mainadmin)
- `GET /api/Auth/profile` - Get current user profile (works for both customers and system users)

### Users (System Users)
- `GET /api/User` - List all users (admin/mainadmin only)
- `GET /api/User/{id}` - Get user details
- `POST /api/User` - Create user (admin/mainadmin only)
- `PUT /api/User/{id}` - Update user (admin/mainadmin or own profile)
- `DELETE /api/User/{id}` - Delete user (mainadmin only)
- `POST /api/User/{id}/change-password` - Change password
- `GET /api/User/profile` - Get current user profile

### Rental Companies
- `GET /api/RentalCompanies` - List companies
- `GET /api/RentalCompanies/{id}` - Get company details
- `POST /api/RentalCompanies` - Create company
- `PUT /api/RentalCompanies/{id}` - Update company
- `DELETE /api/RentalCompanies/{id}` - Delete company
- `GET /api/RentalCompanies/{id}/statistics` - Company statistics

### Customers
- `GET /api/Customers` - List customers
- `GET /api/Customers/{id}` - Get customer details
- `POST /api/Customers` - Create customer
- `PUT /api/Customers/{id}` - Update customer
- `DELETE /api/Customers/{id}` - Delete customer
- `GET /api/Customers/{id}/reservations` - Customer reservations
- `GET /api/Customers/{id}/payment-methods` - Customer payment methods
- `POST /api/Customers/{id}/verify` - Verify customer

### Vehicles
- `GET /api/Vehicles` - List vehicles
- `GET /api/Vehicles/{id}` - Get vehicle details
- `POST /api/Vehicles` - Create vehicle
- `PUT /api/Vehicles/{id}` - Update vehicle
- `DELETE /api/Vehicles/{id}` - Delete vehicle

### Bookings
- `GET /api/Booking/bookings` - List bookings
- `GET /api/Booking/bookings/{id}` - Get booking details
- `POST /api/Booking/bookings` - Create booking
- `PUT /api/Booking/bookings/{id}` - Update booking
- `PATCH /api/Booking/bookings/{id}/status` - Update booking status
- `POST /api/Booking/bookings/{id}/cancel` - Cancel booking
- `GET /api/Booking/bookings/booking-number/{bookingNumber}` - Lookup by booking number
- `DELETE /api/Booking/bookings/{id}` - Delete booking

### Rentals
- `GET /api/Rentals` - List rentals
- `GET /api/Rentals/{id}` - Get rental details
- `POST /api/Rentals` - Start rental
- `PUT /api/Rentals/{id}` - Update rental
- `POST /api/Rentals/{id}/complete` - Complete rental
- `GET /api/Rentals/active` - Active rentals
- `GET /api/Rentals/overdue` - Overdue rentals

### Payments
- `GET /api/Payments` - List payments
- `GET /api/Payments/{id}` - Get payment details
- `POST /api/Payments` - Create payment
- `PUT /api/Payments/{id}` - Update payment
- `POST /api/Payments/{id}/process` - Process payment
- `POST /api/Payments/{id}/succeed` - Mark payment as succeeded
- `POST /api/Payments/{id}/fail` - Mark payment as failed

### Reviews
- `GET /api/Reviews` - List reviews
- `GET /api/Reviews/{id}` - Get review details
- `POST /api/Reviews` - Create review
- `PUT /api/Reviews/{id}` - Update review
- `DELETE /api/Reviews/{id}` - Delete review
- `GET /api/Reviews/vehicle/{vehicleId}` - Vehicle reviews
- `GET /api/Reviews/company/{companyId}` - Company reviews

### Vehicle Categories
- `GET /api/VehicleCategories` - List categories
- `GET /api/VehicleCategories/{id}` - Get category details
- `POST /api/VehicleCategories` - Create category
- `PUT /api/VehicleCategories/{id}` - Update category
- `DELETE /api/VehicleCategories/{id}` - Delete category
- `GET /api/VehicleCategories/{id}/vehicles` - Vehicles in category
- `GET /api/VehicleCategories/{id}/statistics` - Category statistics

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- PostgreSQL database
- Stripe account (for payment processing)

### Configuration

1. Update `appsettings.json` with your database connection string
2. Configure Stripe settings in the configuration
3. Set up JWT secret key for authentication

### Running the Application

```bash
dotnet run --urls "https://localhost:7001;http://localhost:5001"
```

### Swagger Documentation

Access the API documentation at:
- HTTPS: `https://localhost:7001`
- HTTP: `http://localhost:5001`

## Authentication

All API endpoints require Bearer token authentication. To use the API:

1. Sign up or sign in using the `/api/Auth/signup` or `/api/Auth/signin` endpoints
2. Use the returned JWT token in the Authorization header: `Bearer {token}`
3. In Swagger UI, click the "Authorize" button and enter your token

## Database Setup

Run the SQL script in `car_rental_schema.sql` to set up the database schema.

## License

Copyright (c) 2025 Alexander Orlov.
All rights reserved.
