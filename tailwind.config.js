module.exports = {
    darkMode: "class",
    content: [
        "./Views/**/*.cshtml",
        "./Areas/**/*.cshtml",
        "./Pages/**/*.cshtml",
        "./wwwroot/**/*.js",
    ],
    theme: {
        extend: {
            fontFamily: {
                sans: ['"Open Sans"', 'sans-serif'],
            },
            keyframes: {
                floatPulse: {
                    "0%, 100%": { transform: "translateY(-4px) scale(1.05)", boxShadow: "0 10px 25px -5px rgba(0,0,0,0.2), 0 0 0 4px white" },
                    "50%": { transform: "translateY(-6px) scale(1.06)", boxShadow: "0 20px 35px -10px rgba(0,0,0,0.25), 0 0 0 4px white, 0 0 24px 2px rgba(255,255,255,0.4)" },
                },
            },
            animation: {
                "float-pulse": "floatPulse 1.2s ease-in-out infinite",
            },
        },
    },
    plugins: [],
};
