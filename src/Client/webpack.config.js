var path = require("path");
var webpack = require("webpack");
var MinifyPlugin = require("terser-webpack-plugin");
var MonacoWebpackPlugin = require('monaco-editor-webpack-plugin');

function resolve(filePath) {
    return path.join(__dirname, filePath)
}

var CONFIG = {
    fsharpEntry: {
        "app": [
            "whatwg-fetch",
            "@babel/polyfill",
            resolve("./Client.fsproj")
        ]
    },
    devServerProxy: {
        '/api/*': {
            target: 'https://localhost:' + (process.env.SUAVE_FABLE_PORT || "8086"),
            secure: false,
            changeOrigin: true
        }
    },
    historyApiFallback: {
        index: resolve("./index.html")
    },
    contentBase: resolve("./public"),
    // Use babel-preset-env to generate JS compatible with most-used browsers.
    // More info at https://github.com/babel/babel/blob/master/packages/babel-preset-env/README.md
    babel: {
        presets: [
            ["@babel/preset-env", {
                "targets": {
                    "browsers": ["last 2 versions"]
                },
                "modules": false,
                "useBuiltIns": "usage",
            }]
        ],
        plugins: ["@babel/plugin-transform-runtime"]
    }
}

var isProduction = process.argv.indexOf("-p") >= 0;
console.log("Bundling for " + (isProduction ? "production" : "development") + "...");

module.exports = {
    entry : CONFIG.fsharpEntry,
    output: {
        path: resolve('./public/js'),
        publicPath: "/js",
        filename: "[name].js"
    },
    mode: isProduction ? "production" : "development",
    devtool: isProduction ? undefined : "source-map",
    resolve: {
        symlinks: false
    },
    optimization: {
        // Split the code coming from npm packages into a different file.
        // 3rd party dependencies change less often, let the browser cache them.
        splitChunks: {
            cacheGroups: {
                commons: {
                    test: /node_modules/,
                    name: "vendors",
                    chunks: "all"
                }
            }
        },
        minimizer: isProduction ? [new MinifyPlugin()] : []
    },
    // DEVELOPMENT
    //      - HotModuleReplacementPlugin: Enables hot reloading when code changes without refreshing
    plugins: [
        ...(isProduction ? [] : [ new webpack.HotModuleReplacementPlugin() ]),
        ...(isProduction ? [] : [ new webpack.NamedModulesPlugin() ]),
        new MonacoWebpackPlugin()
    ],
    // Configuration for webpack-dev-server
    devServer: {
        proxy: CONFIG.devServerProxy,
        hot: true,
        inline: true,
        historyApiFallback: CONFIG.historyApiFallback,
        contentBase: CONFIG.contentBase
    },
    module: {
        rules: [
            {
                test: /\.fs(x|proj)?$/,
                use: "fable-loader"
            },
            {
                test: /\.js$/,
                exclude: /node_modules/,
                use: {
                    loader: 'babel-loader',
                    options: CONFIG.babel
                },
            },
            {
                test: /\.s(a|c)ss$/,
                use: [
                    "style-loader",
                    "css-loader",
                    "sass-loader"
                ]
            },
            {
                test: /\.css$/,
                use: [
                    "style-loader",
                    "css-loader"
                ]
            },
            {
                test: /\.(eot|svg|ttf|woff|woff2)(\?|$)/,
                use: {
                    loader: 'url-loader',
                    options: {
                        name: '[path][name].[ext]'
                    }
                }
            }
        ]
    }
};
