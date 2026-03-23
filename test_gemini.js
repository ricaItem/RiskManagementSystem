const apiKey = "AIzaSyBMQLDQ6HEG71McMR3yWzZXDh0Lw-jMdQw";
async function test() {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=${apiKey}`;
    const payload = {
        contents: [ { parts: [ { text: "Hello, this is a test prompt." } ] } ]
    };
    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        console.log("Status:", response.status);
        console.log("Response:", await response.text());
    } catch (e) {
        console.error("Error:", e);
    }
}
test();
