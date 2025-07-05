import axios from 'axios';

const API_URL = 'https://localhost:7037/api/Chat/send';

export async function sendMessage(userMessage, sessionId) {
  try {
    const res = await axios.post(API_URL, {
      userMessage,
      sessionId,
    });
    if (res.data && res.data.data) {
      return {
        data: res.data.data,
        sessionId: res.data.sessionId || sessionId,
        error: null,
      };
    } else {
      return {
        data: null,
        sessionId,
        error: 'No response from server.',
      };
    }
  } catch (error) {
    return {
      data: null,
      sessionId,
      error: error.message || 'An error occurred.',
    };
  }
}

export async function fetchWelcome() {
  return sendMessage('__get_welcome__', null);
} 