import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, TouchableOpacity, Alert } from 'react-native';
import { Camera, useCameraDevice, useCodeScanner } from 'react-native-vision-camera';

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

interface DriverLicenseScannerProps {
  onScanComplete: (data: LicenseData) => void;
  apiEndpoint?: string;
}

const DriverLicenseScanner: React.FC<DriverLicenseScannerProps> = ({
  onScanComplete,
  apiEndpoint
}) => {
  const [hasPermission, setHasPermission] = useState(false);
  const [isScanning, setIsScanning] = useState(true);
  const device = useCameraDevice('back');

  useEffect(() => {
    (async () => {
      const status = await Camera.requestCameraPermission();
      setHasPermission(status === 'granted');
    })();
  }, []);

  const parsePDF417Barcode = (rawData: string): LicenseData => {
    const data: LicenseData = { rawData };
    const lines = rawData.split('\n');

    for (const line of lines) {
      const trimmedLine = line.trim();
      
      if (trimmedLine.startsWith('DAC')) {
        data.firstName = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DCS')) {
        data.lastName = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DAD')) {
        data.middleName = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DBB')) {
        const dob = trimmedLine.substring(3).trim();
        data.dateOfBirth = formatDate(dob);
      } else if (trimmedLine.startsWith('DAQ')) {
        data.licenseNumber = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DBA')) {
        const exp = trimmedLine.substring(3).trim();
        data.expirationDate = formatDate(exp);
      } else if (trimmedLine.startsWith('DBD')) {
        const issue = trimmedLine.substring(3).trim();
        data.issueDate = formatDate(issue);
      } else if (trimmedLine.startsWith('DAG')) {
        data.address = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DAI')) {
        data.city = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DAJ')) {
        data.state = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DAK')) {
        data.zipCode = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DCG')) {
        data.country = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DBC')) {
        data.sex = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DAU')) {
        data.height = trimmedLine.substring(3).trim();
      } else if (trimmedLine.startsWith('DAY')) {
        data.eyeColor = trimmedLine.substring(3).trim();
      }
    }

    return data;
  };

  const formatDate = (dateString: string): string => {
    if (dateString.length === 8) {
      const month = dateString.substring(0, 2);
      const day = dateString.substring(2, 4);
      const year = dateString.substring(4, 8);
      return `${month}/${day}/${year}`;
    }
    return dateString;
  };

  const codeScanner = useCodeScanner({
    codeTypes: ['pdf-417'],
    onCodeScanned: (codes) => {
      if (!isScanning || codes.length === 0) return;
      
      setIsScanning(false);
      const scannedData = codes[0].value;
      
      if (scannedData) {
        const licenseData = parsePDF417Barcode(scannedData);
        onScanComplete(licenseData);
      }
    }
  });

  if (!hasPermission) {
    return (
      <View style={styles.container}>
        <Text style={styles.message}>Camera permission is required</Text>
      </View>
    );
  }

  if (device == null) {
    return (
      <View style={styles.container}>
        <Text style={styles.message}>No camera device found</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Camera
        style={StyleSheet.absoluteFill}
        device={device}
        isActive={isScanning}
        codeScanner={codeScanner}
      />
      <View style={styles.overlay}>
        <View style={styles.scanArea} />
        <Text style={styles.instructions}>
          Align barcode on back of license
        </Text>
      </View>
      <TouchableOpacity 
        style={styles.cancelButton}
        onPress={() => setIsScanning(false)}
      >
        <Text style={styles.cancelButtonText}>Cancel</Text>
      </TouchableOpacity>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'black',
  },
  message: {
    textAlign: 'center',
    paddingTop: 100,
    color: 'white',
    fontSize: 18,
  },
  overlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    justifyContent: 'center',
    alignItems: 'center',
  },
  scanArea: {
    width: 300,
    height: 200,
    borderWidth: 2,
    borderColor: '#00ff00',
    backgroundColor: 'transparent',
  },
  instructions: {
    marginTop: 20,
    color: 'white',
    fontSize: 16,
    textAlign: 'center',
  },
  cancelButton: {
    position: 'absolute',
    bottom: 50,
    alignSelf: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.3)',
    paddingHorizontal: 30,
    paddingVertical: 15,
    borderRadius: 25,
  },
  cancelButtonText: {
    color: 'white',
    fontSize: 18,
    fontWeight: 'bold',
  },
});

export default DriverLicenseScanner;
