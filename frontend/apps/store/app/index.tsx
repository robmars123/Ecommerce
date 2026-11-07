import { Redirect } from 'expo-router';

/** The store opens on Shop (the product list). */
export default function IndexScreen() {
  return <Redirect href="/products" />;
}
