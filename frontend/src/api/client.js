import axios from "axios";

const client = axios.create({
  baseURL: "http://localhost:5105/api",
});

client.interceptors.request.use((config) => {
  const token = localStorage.getItem("veriqo_token");
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

client.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401 && !window.location.pathname.includes("login")) {
      localStorage.removeItem("veriqo_token");
      localStorage.removeItem("veriqo_user");
      window.location.href = "/login";
    }
    return Promise.reject(err);
  }
);

export default client;
