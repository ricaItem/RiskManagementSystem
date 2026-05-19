const apiKey = "AIzaSyBMQLDQ6HEG71McMR3yWzZXDh0Lw-jMdQw";
async function test(modelName) {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${modelName}:generateContent?key=${apiKey}`;
    const payload = {
        contents: [ { parts: [ { text: "Hello, this is a test prompt." } ] } ]
    };
    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        console.log(`Status:`, response.status);
        console.log(await response.text());
    } catch (e) {}
}
test("gemini-flash-latest");
