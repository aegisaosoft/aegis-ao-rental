/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov Aegis AO Soft
 *
 */

// Test data for user authentication
const testUser = {
    email: "orlovus@gmail.com",
    password: "Kis@1963"
};

// Test user login
async function testUserLogin() {
    try {
        const response = await fetch('https://localhost:7183/api/Auth/login-user', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(testUser)
        });

        if (response.ok) {
            const data = await response.json();
            console.log('✅ User login successful:', data);
            return data.token;
        } else {
            const error = await response.text();
            console.error('❌ User login failed:', error);
            return null;
        }
    } catch (error) {
        console.error('❌ Network error:', error);
        return null;
    }
}

// Test user profile
async function testUserProfile(token) {
    try {
        const response = await fetch('https://localhost:7183/api/Auth/profile', {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json',
            }
        });

        if (response.ok) {
            const data = await response.json();
            console.log('✅ User profile retrieved:', data);
            return data;
        } else {
            const error = await response.text();
            console.error('❌ Profile retrieval failed:', error);
            return null;
        }
    } catch (error) {
        console.error('❌ Network error:', error);
        return null;
    }
}

// Test user management endpoints (requires admin token)
async function testUserManagement(token) {
    try {
        // Test getting all users
        const response = await fetch('https://localhost:7183/api/User', {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json',
            }
        });

        if (response.ok) {
            const data = await response.json();
            console.log('✅ Users list retrieved:', data);
            return true;
        } else {
            const error = await response.text();
            console.error('❌ Users list retrieval failed:', error);
            return false;
        }
    } catch (error) {
        console.error('❌ Network error:', error);
        return false;
    }
}

// Run all tests
async function runTests() {
    console.log('🚀 Starting User Authentication Tests...\n');
    
    // Test 1: User Login
    console.log('Test 1: User Login');
    const token = await testUserLogin();
    
    if (!token) {
        console.log('❌ Cannot proceed without authentication token\n');
        return;
    }
    
    console.log('');
    
    // Test 2: User Profile
    console.log('Test 2: User Profile');
    const profile = await testUserProfile(token);
    
    console.log('');
    
    // Test 3: User Management (if user is admin/mainadmin)
    console.log('Test 3: User Management');
    await testUserManagement(token);
    
    console.log('\n✅ All tests completed!');
}

// Run tests if this script is executed directly
if (typeof window === 'undefined') {
    // Node.js environment
    runTests();
} else {
    // Browser environment
    console.log('Run runTests() in the browser console to test authentication');
}
