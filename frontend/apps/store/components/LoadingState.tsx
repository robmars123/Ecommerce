import { ActivityIndicator, StyleSheet, View } from 'react-native';

export function LoadingState() {
  return (
    <View style={styles.container}>
      <ActivityIndicator accessibilityLabel="Loading" size="large" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { padding: 24, alignItems: 'center' },
});
