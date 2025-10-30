import React, { useState } from 'react';
import { SafeAreaView, StyleSheet, Text, View, Button, Alert } from 'react-native';
import DriverLicenseScanner from './DriverLicenseScanner';

interface LicenseData {
  firstName?: string;
  lastName?: string;
  middleName?: string;
  dateOfBirth?: string;
  licenseNumber?: string;
  expirationDate?: string;
  issueDate?: string;
  address?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  country?: string;
  sex?: string;
  height?: string;
  eyeColor?: string;
  rawData?: string;
}

const App = () => {
  const [showScanner, setShowScanner] = useState(false);
  const [customerData, setCustomerData] = useState<LicenseData | null>(null);

  const handleScanComplete = async (data: LicenseData) => {
    console.log('License scanned:', data);
    setCustomerData(data);
    setShowScanner(false);

    // Validate age
    if (data.dateOfBirth) {
      const age = calculateAge(data.dateOfBirth);
      
      if (age < 21) {
        Alert.alert(
          'Age Requirement',
          'Customer must be at least 21 years old to rent a vehicle.',
          [{ text: 'OK' }]
        );
        return;
      }
    }

    // Validate expiration date
    if (data.expirationDate) {
      const isExpired = checkIfExpired(data.expirationDate);
      
      if (isExpired) {
        Alert.alert(
          'License Expired',
          'This driver license has expired. Please ask for a valid license.',
          [{ text: 'OK' }]
        );
        return;
      }
    }

    // Send to your API
    try {
      const response = await fetch(
        'https://your-api.azurewebsites.net/api/license/scan',
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            companyId: 'YOUR_COMPANY_ID', // Get from app context
            customerId: 'CUSTOMER_ID', // Get from navigation params
            licenseData: data,
            syncCustomerData: true,
          }),
        }
      );

      if (!response.ok) {
        throw new Error('Failed to save license');
      }

      const result = await response.json();
      
      Alert.alert(
        'Success',
        `License verified for ${data.firstName} ${data.lastName}`,
        [
          {
            text: 'Continue to Rental',
            onPress: () => {
              console.log('Continue to rental booking...');
            },
          },
        ]
      );
    } catch (error) {
      console.error('Error saving license:', error);
      Alert.alert('Error', 'Failed to save license information');
    }
  };

  const calculateAge = (dateOfBirth: string): number => {
    const dob = new Date(dateOfBirth);
    const today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const monthDiff = today.getMonth() - dob.getMonth();
    
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
      age--;
    }
    
    return age;
  };

  const checkIfExpired = (expirationDate: string): boolean => {
    const expDate = new Date(expirationDate);
    const today = new Date();
    return expDate < today;
  };

  if (showScanner) {
    return (
      <DriverLicenseScanner
        onScanComplete={handleScanComplete}
        apiEndpoint="https://your-api.azurewebsites.net/api/license/scan"
      />
    );
  }

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Driver License Scanner</Text>
        <Text style={styles.subtitle}>Rental Platform</Text>
        
        <Button
          title="Scan License"
          onPress={() => setShowScanner(true)}
        />

        {customerData && (
          <View style={styles.customerData}>
            <Text style={styles.dataTitle}>Scanned Data:</Text>
            <Text>Name: {customerData.firstName} {customerData.lastName}</Text>
            <Text>License #: {customerData.licenseNumber}</Text>
            <Text>State: {customerData.state}</Text>
            <Text>DOB: {customerData.dateOfBirth}</Text>
            <Text>Expires: {customerData.expirationDate}</Text>
          </View>
        )}
      </View>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  content: {
    flex: 1,
    padding: 20,
    justifyContent: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 10,
  },
  subtitle: {
    fontSize: 18,
    textAlign: 'center',
    marginBottom: 40,
    color: '#666',
  },
  customerData: {
    marginTop: 30,
    padding: 20,
    backgroundColor: 'white',
    borderRadius: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  dataTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 10,
  },
});

export default App;
